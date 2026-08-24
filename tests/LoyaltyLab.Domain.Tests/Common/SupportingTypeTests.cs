using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Tests.Common;

public sealed class CurrencyAndPercentTests
{
    [Fact]
    public void Currency_codes_are_normalized_to_uppercase()
    {
        Currency.Of("usd").Should().Be(Currency.Usd);
    }

    [Fact]
    public void Currency_rejects_non_iso_codes()
    {
        var act = () => Currency.Of("US");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Percent_as_fraction_does_not_round()
    {
        Percent.From(12m).AsFraction().Should().Be(0.12m);
        Percent.From(-3m).AsFraction().Should().Be(-0.03m);
    }
}

public sealed class EntityIdTests
{
    [Fact]
    public void New_ids_are_unique()
    {
        PartnerId.New().Should().NotBe(PartnerId.New());
    }

    [Fact]
    public void Ids_of_different_types_cannot_be_assigned_to_each_other()
    {
        // Compile-time guarantee: PartnerId and MemberId are distinct types.
        PartnerId partner = PartnerId.New();
        MemberId member = MemberId.New();

        partner.Value.Should().NotBe(member.Value);
    }
}

public sealed class ClockContractTests
{
    [Fact]
    public void IClock_reports_the_instant_it_was_given()
    {
        var instant = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        IClock clock = new StubClock(instant);

        clock.UtcNow.Should().Be(instant);
    }

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
