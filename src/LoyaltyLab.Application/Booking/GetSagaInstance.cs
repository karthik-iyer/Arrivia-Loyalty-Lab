using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Booking;

public sealed class GetSagaInstance(
    ITenantContextAccessor tenant,
    ISagaRepository sagas,
    IPoisonMessageQuery poison) : IUseCase<GetSagaInstanceQuery, SagaOperatorDetail>
{
    public async Task<Result<SagaOperatorDetail>> ExecuteAsync(
        GetSagaInstanceQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (tenant.Current.Role != AccessRole.Operator)
        {
            return Result<SagaOperatorDetail>.Failure(Errors.RoleNotPermitted);
        }

        var saga = await sagas.GetByIdAsync(request.SagaId, cancellationToken);
        if (saga is null)
        {
            return Result<SagaOperatorDetail>.Failure(Errors.SagaNotFound);
        }

        var poisoned = await poison.ListByCorrelationIdAsync(saga.CorrelationId, cancellationToken);
        return Result<SagaOperatorDetail>.Success(
            new SagaOperatorDetail(
                SagaSummary.From(saga),
                saga.BookingId,
                saga.StartedAt,
                saga.LastHeartbeatAt,
                saga.CompletedAt,
                poisoned.Select(message => new PoisonHttpItem(
                    message.Id,
                    message.Type,
                    message.CorrelationId,
                    message.Attempts,
                    message.LastError,
                    message.PoisonedAt)).ToArray()));
    }
}

public sealed class ListSagas(
    ITenantContextAccessor tenant,
    ISagaRepository sagas) : IUseCase<ListSagasQuery, IReadOnlyList<SagaListItem>>
{
    public async Task<Result<IReadOnlyList<SagaListItem>>> ExecuteAsync(
        ListSagasQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (tenant.Current.Role != AccessRole.Operator)
        {
            return Result<IReadOnlyList<SagaListItem>>.Failure(Errors.RoleNotPermitted);
        }

        var rows = await sagas.ListAsync(cancellationToken);
        IReadOnlyList<SagaListItem> items = rows
            .Select(saga => new SagaListItem(
                saga.Id,
                saga.BookingId,
                saga.Status,
                saga.StartedAt,
                saga.LastHeartbeatAt))
            .ToArray();
        return Result<IReadOnlyList<SagaListItem>>.Success(items);
    }
}
