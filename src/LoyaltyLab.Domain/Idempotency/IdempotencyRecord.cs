using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Idempotency;

/// <summary>
/// One reserved (partner, operation, key). Payload hash detects a reused key with a different body (FR-L-05).
/// </summary>
public sealed class IdempotencyRecord : ITenantOwned
{
    private IdempotencyRecord()
    {
        Operation = null!;
        Key = null!;
        PayloadHash = null!;
    }

    public IdempotencyRecord(
        PartnerId partnerId,
        string operation,
        string key,
        string payloadHash,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new DomainException("Idempotency operation is required.");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainException("Idempotency key is required.");
        }

        if (string.IsNullOrWhiteSpace(payloadHash))
        {
            throw new DomainException("Payload hash is required.");
        }

        PartnerId = partnerId;
        Operation = operation.Trim();
        Key = key.Trim();
        PayloadHash = payloadHash.Trim();
        CreatedAt = createdAt;
    }

    public PartnerId PartnerId { get; private set; }

    public string Operation { get; private set; }

    public string Key { get; private set; }

    public string PayloadHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool MatchesPayload(string payloadHash) =>
        string.Equals(PayloadHash, payloadHash, StringComparison.Ordinal);
}
