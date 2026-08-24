using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Domain.Tests.Ledger;

public sealed class LedgerTransactionTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Unbalanced_construction_throws()
    {
        var member = LedgerAccount.MemberCredits(PartnerId.New(), MemberId.New());
        var issuance = LedgerAccount.Issuance(member.PartnerId);
        var clock = new MutableClock(AsOf);

        var act = () => LedgerTransaction.Create(
            member.PartnerId,
            LedgerTransactionType.Earn,
            [new LedgerEntry(member.Id, 500), new LedgerEntry(issuance.Id, -400)],
            "earn-1",
            "Opening grant",
            clock);

        act.Should().Throw<DomainException>().WithMessage("*LEDGER_UNBALANCED*");
    }

    [Fact]
    public void Earn_burn_and_expire_keep_every_transaction_and_the_books_at_zero()
    {
        var books = Books.Open();
        var earn = LedgerTransaction.Earn(books.Member, books.Issuance, 500, "earn-1", "Opening grant", books.Clock);
        var burn = LedgerTransaction.Burn(books.Member, books.Redemption, 200, "burn-1", "Booking tender", books.Clock);
        var expire = LedgerTransaction.Expire(books.Member, books.Breakage, 50, "expire-1", "Lapsed", books.Clock);

        Sum(earn).Should().Be(0);
        Sum(burn).Should().Be(0);
        Sum(expire).Should().Be(0);
        Balance(books.Member, earn, burn, expire).Should().Be(250);
        Balance(books.Issuance, earn, burn, expire).Should().Be(-500);
        Balance(books.Redemption, earn, burn, expire).Should().Be(200);
        Balance(books.Breakage, earn, burn, expire).Should().Be(50);
        (Balance(books.Issuance, earn, burn, expire)
            + Balance(books.Redemption, earn, burn, expire)
            + Balance(books.Breakage, earn, burn, expire)
            + Balance(books.Member, earn, burn, expire)).Should().Be(0);
    }

    [Fact]
    public void Reversal_mirrors_the_original_legs()
    {
        var books = Books.Open();
        var earn = LedgerTransaction.Earn(books.Member, books.Issuance, 500, "earn-1", "Opening grant", books.Clock);
        var reversal = LedgerTransaction.Reverse(earn, "rev-1", "Clawback", books.Clock);

        reversal.Type.Should().Be(LedgerTransactionType.Reversal);
        reversal.ReversesTransactionId.Should().Be(earn.Id);
        reversal.Entries.Select(e => e.Amount).Should().Equal(earn.Entries.Select(e => -e.Amount));
        reversal.Entries.Select(e => e.AccountId).Should().Equal(earn.Entries.Select(e => e.AccountId));
        Balance(books.Member, earn, reversal).Should().Be(0);
        Sum(reversal).Should().Be(0);
    }

    [Fact]
    public void A_reversal_cannot_be_reversed()
    {
        var books = Books.Open();
        var earn = LedgerTransaction.Earn(books.Member, books.Issuance, 500, "earn-1", "Opening grant", books.Clock);
        var reversal = LedgerTransaction.Reverse(earn, "rev-1", "Clawback", books.Clock);

        var act = () => LedgerTransaction.Reverse(reversal, "rev-2", "Undo the undo", books.Clock);

        act.Should().Throw<DomainException>().WithMessage("*cannot itself be reversed*");
    }

    [Fact]
    public void Adjustment_moves_the_same_two_accounts_as_an_earn()
    {
        var books = Books.Open();
        var adjustment = LedgerTransaction.Adjust(books.Member, books.Issuance, -40, "adj-1", "Goodwill correction", books.Clock);

        adjustment.Type.Should().Be(LedgerTransactionType.Adjustment);
        Balance(books.Member, adjustment).Should().Be(-40);
        Balance(books.Issuance, adjustment).Should().Be(40);
        Sum(adjustment).Should().Be(0);
    }

    [Fact]
    public void Wrong_account_type_is_rejected()
    {
        var books = Books.Open();
        var act = () => LedgerTransaction.Earn(books.Member, books.Redemption, 10, "earn-1", "Bad", books.Clock);

        act.Should().Throw<DomainException>().WithMessage("*PartnerIssuance*");
    }

    private static int Sum(LedgerTransaction transaction) => transaction.Entries.Sum(e => e.Amount);

    private static int Balance(LedgerAccount account, params LedgerTransaction[] transactions) =>
        transactions.SelectMany(t => t.Entries).Where(e => e.AccountId == account.Id).Sum(e => e.Amount);

    private sealed class Books
    {
        private Books(LedgerAccount member, LedgerAccount issuance, LedgerAccount redemption, LedgerAccount breakage, MutableClock clock)
        {
            Member = member;
            Issuance = issuance;
            Redemption = redemption;
            Breakage = breakage;
            Clock = clock;
        }

        public LedgerAccount Member { get; }

        public LedgerAccount Issuance { get; }

        public LedgerAccount Redemption { get; }

        public LedgerAccount Breakage { get; }

        public MutableClock Clock { get; }

        public static Books Open()
        {
            var partner = PartnerId.New();
            return new Books(
                LedgerAccount.MemberCredits(partner, MemberId.New()),
                LedgerAccount.Issuance(partner),
                LedgerAccount.Redemption(partner),
                LedgerAccount.Breakage(partner),
                new MutableClock(AsOf));
        }
    }
}
