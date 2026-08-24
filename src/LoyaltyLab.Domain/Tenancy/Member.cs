using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Tenancy;

/// <summary>
/// Anything owned by a partner. Persistence applies a global query filter on this.
/// </summary>
public interface ITenantOwned
{
    PartnerId PartnerId { get; }
}

public sealed class Member : Entity<MemberId>, ITenantOwned
{
    private Member()
    {
    }

    private Member(MemberId id, PartnerId partnerId, string displayName, TierCode tier, bool isActive)
        : base(id)
    {
        PartnerId = partnerId;
        DisplayName = displayName;
        Tier = tier;
        IsActive = isActive;
    }

    public PartnerId PartnerId { get; private set; }

    public string DisplayName { get; private set; } = null!;

    public TierCode Tier { get; private set; }

    public bool IsActive { get; private set; }

    public static Member Create(PartnerId partnerId, string displayName, TierCode tier, bool isActive = true, MemberId? id = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("Member display name is required.");
        }

        return new Member(id ?? MemberId.New(), partnerId, displayName.Trim(), tier, isActive);
    }
}

/// <summary>
/// Resolved once per request and passed explicitly. Never re-fetched ad hoc deeper in the stack (FR-X-03).
/// </summary>
public sealed record TenantContext
{
    public TenantContext(PartnerId partnerId, MemberId? memberId, TierCode? tier, AccessRole role)
    {
        if (role is AccessRole.Member && memberId is null)
        {
            throw new DomainException("A member-role context must include a member id.");
        }

        if (role is AccessRole.Anonymous && memberId is not null)
        {
            throw new DomainException("An anonymous context cannot carry a member id.");
        }

        PartnerId = partnerId;
        MemberId = memberId;
        Tier = tier;
        Role = role;
    }

    public PartnerId PartnerId { get; }

    public MemberId? MemberId { get; }

    public TierCode? Tier { get; }

    public AccessRole Role { get; }

    public static TenantContext Anonymous(PartnerId partnerId) =>
        new(partnerId, memberId: null, tier: null, AccessRole.Anonymous);

    public static TenantContext ForMember(Member member) =>
        new(member.PartnerId, member.Id, member.Tier, AccessRole.Member);

    public static TenantContext ForRole(PartnerId partnerId, AccessRole role) =>
        role switch
        {
            AccessRole.Anonymous => Anonymous(partnerId),
            AccessRole.Member => throw new DomainException("A member-role context must include a member id."),
            _ => new(partnerId, memberId: null, tier: null, role),
        };

    public bool HasMember => MemberId is not null;
}
