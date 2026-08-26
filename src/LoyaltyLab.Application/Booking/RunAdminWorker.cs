using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Application.Opportunity;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Booking;

public sealed class RunAdminWorker(
    ITenantContextAccessor tenant,
    IOutboxDispatch outbox,
    RecoverStalledSagas recover,
    ExpireDueCredits expire,
    ScanOpportunities scan) : IUseCase<RunAdminWorkerCommand, RunAdminWorkerResult>
{
    public async Task<Result<RunAdminWorkerResult>> ExecuteAsync(
        RunAdminWorkerCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (tenant.Current.Role != AccessRole.Operator)
        {
            return Result<RunAdminWorkerResult>.Failure(Errors.RoleNotPermitted);
        }

        var name = request.Worker.Trim().ToLowerInvariant();
        switch (name)
        {
            case "outbox":
                return Result<RunAdminWorkerResult>.Success(
                    new RunAdminWorkerResult(name, await outbox.DispatchAsync(cancellationToken)));
            case "recovery":
                return Result<RunAdminWorkerResult>.Success(
                    new RunAdminWorkerResult(name, await recover.ExecuteAsync(cancellationToken)));
            case "expiry":
                var expired = await expire.ExecuteAsync(new ExpireDueCreditsCommand(), cancellationToken);
                return expired.IsFailure
                    ? Result<RunAdminWorkerResult>.Failure(expired.Error)
                    : Result<RunAdminWorkerResult>.Success(
                        new RunAdminWorkerResult(name, expired.Value.Posted.Count));
            case "scan":
                var scanned = await scan.ExecuteAsync(new ScanOpportunitiesCommand(), cancellationToken);
                return scanned.IsFailure
                    ? Result<RunAdminWorkerResult>.Failure(scanned.Error)
                    : Result<RunAdminWorkerResult>.Success(
                        new RunAdminWorkerResult(name, scanned.Value.MembersScanned));
            default:
                return Result<RunAdminWorkerResult>.Failure(Errors.WorkerNotFound);
        }
    }
}
