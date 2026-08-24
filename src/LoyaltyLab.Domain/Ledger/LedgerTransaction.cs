using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Ledger;

public enum LedgerTransactionType
{
    Earn = 0,
    Burn = 1,
    Expire = 2,
    Reversal = 3,
    Adjustment = 4,
}

/// <summary>
/// One signed movement against an account. Positive amounts increase that account's derived balance.
/// </summary>
public sealed class LedgerEntry
{
    private LedgerEntry()
    {
    }

    public LedgerEntry(LedgerAccountId accountId, int amount)
    {
        if (amount == 0)
        {
            throw new DomainException("A ledger entry cannot be zero; omit the leg instead.");
        }

        AccountId = accountId;
        Amount = amount;
    }

    public LedgerAccountId AccountId { get; private set; }

    public int Amount { get; private set; }

    public LedgerEntry Negate() => new(AccountId, -Amount);
}

/// <summary>
/// An append-only, balanced posting (FR-L-01, FR-L-02). Corrections are new transactions, never edits.
/// </summary>
public sealed class LedgerTransaction : Entity<LedgerTransactionId>, ITenantOwned
{
    private LedgerTransaction()
    {
        IdempotencyKey = null!;
        Reason = null!;
        Entries = [];
    }

    private LedgerTransaction(
        LedgerTransactionId id,
        PartnerId partnerId,
        LedgerTransactionType type,
        string idempotencyKey,
        string reason,
        DateTimeOffset occurredAt,
        IReadOnlyList<LedgerEntry> entries,
        LedgerTransactionId? reversesTransactionId,
        BookingId? bookingId)
        : base(id)
    {
        PartnerId = partnerId;
        Type = type;
        IdempotencyKey = idempotencyKey;
        Reason = reason;
        OccurredAt = occurredAt;
        Entries = [.. entries];
        ReversesTransactionId = reversesTransactionId;
        BookingId = bookingId;
    }

    public PartnerId PartnerId { get; private set; }

    public LedgerTransactionType Type { get; private set; }

    public string IdempotencyKey { get; private set; }

    public LedgerTransactionId? ReversesTransactionId { get; private set; }

    public BookingId? BookingId { get; private set; }

    public string Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public List<LedgerEntry> Entries { get; private set; }

    public static LedgerTransaction Earn(
        LedgerAccount memberCredits,
        LedgerAccount issuance,
        int credits,
        string idempotencyKey,
        string reason,
        IClock clock,
        BookingId? bookingId = null)
    {
        RequirePositive(credits);
        return Post(
            LedgerTransactionType.Earn,
            Pair(memberCredits, LedgerAccountType.MemberCredits, credits, issuance, LedgerAccountType.PartnerIssuance, -credits),
            idempotencyKey,
            reason,
            clock,
            bookingId: bookingId,
            partnerId: memberCredits.PartnerId);
    }

    public static LedgerTransaction Burn(
        LedgerAccount memberCredits,
        LedgerAccount redemption,
        int credits,
        string idempotencyKey,
        string reason,
        IClock clock,
        BookingId? bookingId = null)
    {
        RequirePositive(credits);
        return Post(
            LedgerTransactionType.Burn,
            Pair(memberCredits, LedgerAccountType.MemberCredits, -credits, redemption, LedgerAccountType.PartnerRedemption, credits),
            idempotencyKey,
            reason,
            clock,
            bookingId: bookingId,
            partnerId: memberCredits.PartnerId);
    }

    public static LedgerTransaction Expire(
        LedgerAccount memberCredits,
        LedgerAccount breakage,
        int credits,
        string idempotencyKey,
        string reason,
        IClock clock)
    {
        RequirePositive(credits);
        return Post(
            LedgerTransactionType.Expire,
            Pair(memberCredits, LedgerAccountType.MemberCredits, -credits, breakage, LedgerAccountType.PartnerBreakage, credits),
            idempotencyKey,
            reason,
            clock,
            partnerId: memberCredits.PartnerId);
    }

    public static LedgerTransaction Adjust(
        LedgerAccount memberCredits,
        LedgerAccount issuance,
        int credits,
        string idempotencyKey,
        string reason,
        IClock clock) =>
        Post(
            LedgerTransactionType.Adjustment,
            Pair(memberCredits, LedgerAccountType.MemberCredits, credits, issuance, LedgerAccountType.PartnerIssuance, -credits),
            idempotencyKey,
            reason,
            clock,
            partnerId: memberCredits.PartnerId);

    public static LedgerTransaction Reverse(
        LedgerTransaction original,
        string idempotencyKey,
        string reason,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.Type == LedgerTransactionType.Reversal)
        {
            throw new DomainException("A reversal cannot itself be reversed; post a new adjusting transaction.");
        }

        return Post(
            LedgerTransactionType.Reversal,
            original.Entries.Select(entry => entry.Negate()).ToList(),
            idempotencyKey,
            reason,
            clock,
            reverses: original.Id,
            bookingId: original.BookingId,
            partnerId: original.PartnerId);
    }

    /// <summary>
    /// Low-level constructor used by the typed factories. Unbalanced legs are a defect (FR-L-02).
    /// </summary>
    public static LedgerTransaction Create(
        PartnerId partnerId,
        LedgerTransactionType type,
        IReadOnlyList<LedgerEntry> entries,
        string idempotencyKey,
        string reason,
        IClock clock,
        LedgerTransactionId? reverses = null,
        BookingId? bookingId = null,
        LedgerTransactionId? id = null) =>
        Post(type, entries, idempotencyKey, reason, clock, reverses, bookingId, partnerId, id);

    private static LedgerTransaction Post(
        LedgerTransactionType type,
        IReadOnlyList<LedgerEntry> entries,
        string idempotencyKey,
        string reason,
        IClock clock,
        LedgerTransactionId? reverses = null,
        BookingId? bookingId = null,
        PartnerId? partnerId = null,
        LedgerTransactionId? id = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(clock);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException("Every ledger mutation requires an idempotency key.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A ledger transaction must carry a reason.");
        }

        if (entries.Count < 2)
        {
            throw new DomainException("A ledger transaction needs at least two legs.");
        }

        AssertBalanced(entries);

        if (partnerId is null)
        {
            throw new DomainException("A ledger transaction must belong to a partner.");
        }

        return new LedgerTransaction(
            id ?? LedgerTransactionId.New(),
            partnerId.Value,
            type,
            idempotencyKey.Trim(),
            reason.Trim(),
            clock.UtcNow,
            entries,
            reverses,
            bookingId);
    }

    private static IReadOnlyList<LedgerEntry> Pair(
        LedgerAccount left,
        LedgerAccountType leftType,
        int leftAmount,
        LedgerAccount right,
        LedgerAccountType rightType,
        int rightAmount)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        RequireType(left, leftType);
        RequireType(right, rightType);

        if (left.PartnerId != right.PartnerId)
        {
            throw new DomainException("Both legs of a posting must belong to the same partner.");
        }

        if (leftAmount == 0 || rightAmount == 0)
        {
            throw new DomainException("A credit posting must move a non-zero amount.");
        }

        return
        [
            new LedgerEntry(left.Id, leftAmount),
            new LedgerEntry(right.Id, rightAmount),
        ];
    }

    private static void RequirePositive(int credits)
    {
        if (credits <= 0)
        {
            throw new DomainException("Earn, burn, and expire postings must move a positive credit amount.");
        }
    }

    private static void RequireType(LedgerAccount account, LedgerAccountType expected)
    {
        if (account.Type != expected)
        {
            throw new DomainException($"Expected a {expected} account, not {account.Type}.");
        }
    }

    private static void AssertBalanced(IReadOnlyList<LedgerEntry> entries)
    {
        var sum = 0;
        foreach (var entry in entries)
        {
            sum += entry.Amount;
        }

        if (sum != 0)
        {
            throw new DomainException($"{Errors.LedgerUnbalanced.Code}: {Errors.LedgerUnbalanced.Message}");
        }
    }
}
