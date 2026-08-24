using System.Security.Cryptography;
using System.Text;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Idempotency;

namespace LoyaltyLab.Application.Idempotency;

public static class IdempotencyHash
{
    public static string Compute(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}

public sealed record ClaimIdempotencyCommand(string Operation, string Key, string Payload);

public sealed record IdempotencyClaim(IdempotencyRecord Record, bool IsReplay);

/// <summary>
/// Reserves (partner, operation, key) before a mutation. Same payload is a replay; a different payload is a client defect.
/// </summary>
public sealed class ClaimIdempotency(
    ITenantContextAccessor tenant,
    IIdempotencyStore store,
    IClock clock) : IUseCase<ClaimIdempotencyCommand, IdempotencyClaim>
{
    public async Task<Result<IdempotencyClaim>> ExecuteAsync(
        ClaimIdempotencyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hash = IdempotencyHash.Compute(request.Payload);
        var candidate = new IdempotencyRecord(
            tenant.Current.PartnerId,
            request.Operation,
            request.Key,
            hash,
            clock.UtcNow);

        if (await store.SaveAsync(candidate, cancellationToken))
        {
            return Result<IdempotencyClaim>.Success(new IdempotencyClaim(candidate, IsReplay: false));
        }

        var existing = await store.FindAsync(
            candidate.PartnerId,
            candidate.Operation,
            candidate.Key,
            cancellationToken);

        if (existing is null)
        {
            throw new DomainException("Idempotency save lost the race but the winning row was not found.");
        }

        if (!existing.MatchesPayload(hash))
        {
            return Result<IdempotencyClaim>.Failure(Errors.IdempotencyKeyReused);
        }

        return Result<IdempotencyClaim>.Success(new IdempotencyClaim(existing, IsReplay: true));
    }
}
