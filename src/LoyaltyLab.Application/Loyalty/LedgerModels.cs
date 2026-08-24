using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Application.Loyalty;

public sealed record LedgerPostingResult(LedgerTransaction Transaction, bool IsReplay);

public sealed record EarnCreditsCommand(
    MemberId MemberId,
    int Credits,
    string IdempotencyKey,
    string Reason,
    BookingId? BookingId = null);

public sealed record BurnCreditsCommand(
    MemberId MemberId,
    int Credits,
    Money MemberPrice,
    string IdempotencyKey,
    string Reason,
    BookingId? BookingId = null);

public sealed record ExpireCreditsCommand(
    MemberId MemberId,
    int Credits,
    string IdempotencyKey,
    string Reason);

public sealed record AdjustCreditsCommand(
    MemberId MemberId,
    int Credits,
    string IdempotencyKey,
    string Reason);

public sealed record ReverseLedgerCommand(
    LedgerTransactionId OriginalId,
    string IdempotencyKey,
    string Reason);

public sealed record GetBalanceQuery;

public sealed record MemberBalance(
    MemberId MemberId,
    int Credits,
    Money MonetaryValue,
    Percent BurnCap);

public sealed record GetStatementQuery;

public sealed record StatementLine(
    LedgerTransactionId Id,
    LedgerTransactionType Type,
    DateTimeOffset OccurredAt,
    string Reason,
    int Credits,
    int RunningBalance,
    LedgerTransactionId? ReversesTransactionId);

public sealed record MemberStatement(
    MemberId MemberId,
    int Balance,
    IReadOnlyList<StatementLine> Lines);

public sealed record GetLiabilityReportQuery(DateOnly AsOf);

public sealed record ReconcileLedgerQuery(DateOnly AsOf);

public sealed record ReconciliationReport(
    PartnerId PartnerId,
    DateOnly AsOf,
    int LedgerNetBurns,
    int BookingTenders,
    int Difference,
    bool IsBalanced);

public sealed record ExpireDueCreditsCommand;

public sealed record ExpireDueCreditsResult(IReadOnlyList<LedgerPostingResult> Posted);
