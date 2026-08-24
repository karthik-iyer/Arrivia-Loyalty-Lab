using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Loyalty;

public sealed class ReconcileLedger(
    ITenantContextAccessor tenant,
    ILedgerRepository ledger,
    IBookingTenderQuery bookingTenders) : IUseCase<ReconcileLedgerQuery, ReconciliationReport>
{
    public async Task<Result<ReconciliationReport>> ExecuteAsync(
        ReconcileLedgerQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asOf = LedgerBalances.OnOrBefore(await ledger.ListAsync(cancellationToken), request.AsOf);
        var accounts = await ledger.ListAccountsAsync(cancellationToken);
        var redemption = accounts.SingleOrDefault(account => account.Type == LedgerAccountType.PartnerRedemption);
        var ledgerNetBurns = redemption is null ? 0 : LedgerBalances.For(redemption.Id, asOf);
        var tenders = await bookingTenders.SumSettledCreditTendersAsync(request.AsOf, cancellationToken);
        var difference = ledgerNetBurns - tenders;

        return Result<ReconciliationReport>.Success(
            new ReconciliationReport(
                tenant.Current.PartnerId,
                request.AsOf,
                ledgerNetBurns,
                tenders,
                difference,
                difference == 0));
    }
}
