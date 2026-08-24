using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Ledger;

public enum LedgerAccountType
{
    MemberCredits = 0,
    PartnerIssuance = 1,
    PartnerRedemption = 2,
    PartnerBreakage = 3,
}

/// <summary>
/// A named bucket whose balance is the sum of its entries, never a stored column (FR-L-04).
/// </summary>
public sealed class LedgerAccount : Entity<LedgerAccountId>, ITenantOwned
{
    private LedgerAccount()
    {
    }

    private LedgerAccount(
        LedgerAccountId id,
        PartnerId partnerId,
        LedgerAccountType type,
        MemberId? memberId)
        : base(id)
    {
        if (type == LedgerAccountType.MemberCredits && memberId is null)
        {
            throw new DomainException("A member-credits account must name a member.");
        }

        if (type != LedgerAccountType.MemberCredits && memberId is not null)
        {
            throw new DomainException("A partner ledger account cannot be scoped to a member.");
        }

        PartnerId = partnerId;
        Type = type;
        MemberId = memberId;
    }

    public PartnerId PartnerId { get; private set; }

    public LedgerAccountType Type { get; private set; }

    public MemberId? MemberId { get; private set; }

    public static LedgerAccount MemberCredits(PartnerId partnerId, MemberId memberId, LedgerAccountId? id = null) =>
        new(id ?? LedgerAccountId.New(), partnerId, LedgerAccountType.MemberCredits, memberId);

    public static LedgerAccount Issuance(PartnerId partnerId, LedgerAccountId? id = null) =>
        new(id ?? LedgerAccountId.New(), partnerId, LedgerAccountType.PartnerIssuance, memberId: null);

    public static LedgerAccount Redemption(PartnerId partnerId, LedgerAccountId? id = null) =>
        new(id ?? LedgerAccountId.New(), partnerId, LedgerAccountType.PartnerRedemption, memberId: null);

    public static LedgerAccount Breakage(PartnerId partnerId, LedgerAccountId? id = null) =>
        new(id ?? LedgerAccountId.New(), partnerId, LedgerAccountType.PartnerBreakage, memberId: null);
}
