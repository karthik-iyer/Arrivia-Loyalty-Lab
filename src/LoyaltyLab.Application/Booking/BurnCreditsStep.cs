using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Application.Booking;

public sealed class BurnCreditsStep(BurnCredits burn, ReverseLedger reverse, ILedgerRepository ledger) : ISagaStep
{
    public SagaStepKind Kind => SagaStepKind.BurnCredits;

    public int Order => (int)Kind;

    public async Task<StepOutcome> ExecuteAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Tender.HasCredits)
        {
            return StepOutcome.Succeeded();
        }

        var posted = await burn.ExecuteAsync(
            new BurnCreditsCommand(
                context.Member.Id,
                context.Tender.CreditsApplied,
                context.Quote.MemberPrice,
                context.Key(Kind),
                "Booking tender",
                context.Saga.BookingId),
            cancellationToken);
        return ToStep(posted);
    }

    public async Task<CompensationOutcome> CompensateAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var reference = context.Reference(Kind);
        if (string.IsNullOrWhiteSpace(reference) || !Guid.TryParse(reference, out var id))
        {
            return CompensationOutcome.Ok();
        }

        var reversed = await reverse.ExecuteAsync(
            new ReverseLedgerCommand(
                new LedgerTransactionId(id),
                context.CompensateKey(Kind),
                "Compensate booking burn"),
            cancellationToken);
        return ToCompensation(reversed);
    }

    public async Task<StepOutcome> ResolveUnknownAsync(SagaContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Tender.HasCredits)
        {
            return StepOutcome.Succeeded();
        }

        var existing = await ledger.FindByIdempotencyKeyAsync(context.Key(Kind), cancellationToken);
        return existing is null
            ? await ExecuteAsync(context, cancellationToken)
            : StepOutcome.Succeeded(existing.Id.ToString());
    }

    private static StepOutcome ToStep(Result<LedgerPostingResult> posted) =>
        posted.IsSuccess
            ? StepOutcome.Succeeded(posted.Value.Transaction.Id.ToString())
            : StepOutcome.Failed(posted.Error);

    private static CompensationOutcome ToCompensation(Result<LedgerPostingResult> posted) =>
        posted.IsSuccess
            ? CompensationOutcome.Ok(posted.Value.Transaction.Id.ToString())
            : CompensationOutcome.Fail(posted.Error);
}
