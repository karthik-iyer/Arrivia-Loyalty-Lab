using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Ledger;

public sealed record LiabilityReport(
    PartnerId PartnerId,
    DateOnly AsOf,
    int CreditsIssued,
    int CreditsBurned,
    int CreditsExpired,
    int CreditsOutstanding,
    Money MonetaryLiability);
