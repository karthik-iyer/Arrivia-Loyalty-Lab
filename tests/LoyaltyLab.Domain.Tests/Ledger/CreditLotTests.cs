using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Domain.Tests.Ledger;

public sealed class CreditLotTests
{
    private static readonly DateTimeOffset Day0 = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Burns_consume_the_oldest_lot_first()
    {
        var books = Books.Open();
        books.Clock.UtcNow = Day0;
        var first = LedgerTransaction.Earn(books.Member, books.Issuance, 100, "earn-1", "First", books.Clock);
        books.Clock.UtcNow = Day0.AddDays(4);
        var second = LedgerTransaction.Earn(books.Member, books.Issuance, 50, "earn-2", "Second", books.Clock);
        var burn = LedgerTransaction.Burn(books.Member, books.Redemption, 30, "burn-1", "Tender", books.Clock);

        var lots = CreditLots.Remaining([first, second, burn], books.Member.Id, lifetimeDays: 10);

        lots.Should().HaveCount(2);
        lots[0].Remaining.Should().Be(70);
        lots[0].OpenedAt.Should().Be(Day0);
        lots[1].Remaining.Should().Be(50);
        CreditLots.Due(lots, Day0.AddDays(11)).Should().Be(70);
        CreditLots.Due(lots, Day0.AddDays(14)).Should().Be(120);
    }

    [Fact]
    public void Later_activity_is_ignored_when_slicing_history()
    {
        var books = Books.Open();
        var earn = LedgerTransaction.Earn(books.Member, books.Issuance, 500, "earn-1", "Opening", books.Clock);
        books.Clock.UtcNow = Day0.AddDays(20);
        var extra = LedgerTransaction.Earn(books.Member, books.Issuance, 100, "earn-2", "Later", books.Clock);

        var asOf = LedgerBalances.OnOrBefore([earn, extra], DateOnly.FromDateTime(Day0.UtcDateTime));
        LedgerBalances.For(books.Member.Id, asOf).Should().Be(500);
        LedgerBalances.For(books.Member.Id, [earn, extra]).Should().Be(600);
    }

    private sealed class Books
    {
        private Books(
            LedgerAccount member,
            LedgerAccount issuance,
            LedgerAccount redemption,
            MutableClock clock)
        {
            Member = member;
            Issuance = issuance;
            Redemption = redemption;
            Clock = clock;
        }

        public LedgerAccount Member { get; }

        public LedgerAccount Issuance { get; }

        public LedgerAccount Redemption { get; }

        public MutableClock Clock { get; }

        public static Books Open()
        {
            var partner = PartnerId.New();
            return new Books(
                LedgerAccount.MemberCredits(partner, MemberId.New()),
                LedgerAccount.Issuance(partner),
                LedgerAccount.Redemption(partner),
                new MutableClock(Day0));
        }
    }
}
