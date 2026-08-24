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
