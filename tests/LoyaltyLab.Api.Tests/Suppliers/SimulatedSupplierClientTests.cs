using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Infrastructure.Suppliers;

namespace LoyaltyLab.Api.Tests.Suppliers;

public sealed class SimulatedSupplierClientTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly StayDate = new(2026, 6, 1);

    [Fact]
    public async Task Query_resolves_an_ambiguous_reservation()
    {
        var offer = Offer();
        var client = CreateClient(offer, timeoutOnReserve: true);

        var reserved = await client.ReserveAsync(Request(offer.Id), CancellationToken.None);

        reserved.Result.Should().Be(StepResult.Unknown);
        reserved.ExternalReference.Should().BeNull();
        reserved.Error.Should().BeNull();

        var queried = await client.QueryReservationAsync("saga-1:ReserveInventory", CancellationToken.None);

        queried.Result.Should().Be(StepResult.Succeeded);
        queried.ExternalReference.Should().NotBeNullOrWhiteSpace();
        queried.Error.Should().BeNull();
    }

    [Fact]
    public async Task Reserve_is_success_with_a_reference()
    {
        var offer = Offer();
        var client = CreateClient(offer);

        var outcome = await client.ReserveAsync(Request(offer.Id), CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Succeeded);
        outcome.ExternalReference.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Decline_is_failed_not_unknown()
    {
        var offer = Offer();
        var client = CreateClient(offer, declineOnReserve: true);

        var outcome = await client.ReserveAsync(Request(offer.Id), CancellationToken.None);

        outcome.Result.Should().Be(StepResult.Failed);
        outcome.Error.Should().Be(Errors.SupplierUnavailable);
        outcome.ExternalReference.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Same_key_replays_the_held_reference()
    {
        var offer = Offer();
        var client = CreateClient(offer);

        var first = await client.ReserveAsync(Request(offer.Id), CancellationToken.None);
        var replay = await client.ReserveAsync(Request(offer.Id), CancellationToken.None);

        replay.Result.Should().Be(StepResult.Succeeded);
        replay.ExternalReference.Should().Be(first.ExternalReference);
    }

    [Fact]
    public async Task Same_key_different_payload_is_conflict()
    {
        var offer = Offer();
        var other = Offer();
        var client = CreateClient(offer, other);

        var first = await client.ReserveAsync(Request(offer.Id), CancellationToken.None);
        var reused = await client.ReserveAsync(Request(other.Id), CancellationToken.None);

        first.Result.Should().Be(StepResult.Succeeded);
        reused.Result.Should().Be(StepResult.Failed);
        reused.Error.Should().Be(Errors.IdempotencyKeyReused);
    }

    [Fact]
    public async Task Release_is_idempotent()
    {
        var offer = Offer();
        var client = CreateClient(offer);
        var reserved = await client.ReserveAsync(Request(offer.Id), CancellationToken.None);

        var first = await client.ReleaseAsync(reserved.ExternalReference!, CancellationToken.None);
        var again = await client.ReleaseAsync(reserved.ExternalReference!, CancellationToken.None);

        first.Result.Should().Be(StepResult.Succeeded);
        again.Result.Should().Be(StepResult.Succeeded);

        var queried = await client.QueryReservationAsync("saga-1:ReserveInventory", CancellationToken.None);
        queried.Result.Should().Be(StepResult.Failed);
        queried.Error.Should().Be(Errors.SupplierUnavailable);
    }

    [Fact]
    public async Task FailOnRelease_leaves_the_hold_in_place()
    {
        var offer = Offer();
        var faults = new SupplierFaultHooks();
        var client = new SimulatedSupplierClient(new FakeOffers(offer), faults, new FixedClock(AsOf));
        var reserved = await client.ReserveAsync(Request(offer.Id), CancellationToken.None);
        faults.FailOnRelease = true;

        var released = await client.ReleaseAsync(reserved.ExternalReference!, CancellationToken.None);
        var queried = await client.QueryReservationAsync("saga-1:ReserveInventory", CancellationToken.None);

        reserved.Result.Should().Be(StepResult.Succeeded);
        released.Result.Should().Be(StepResult.Failed);
        released.Error.Should().Be(Errors.SupplierUnavailable);
        queried.Result.Should().Be(StepResult.Succeeded);
    }

    [Fact]
    public async Task Current_net_rate_is_the_offer_rate()
    {
        var offer = Offer();
        var client = CreateClient(offer);

        var result = await client.GetCurrentNetRateAsync(offer.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(offer.NetRate);
    }

    private static SimulatedSupplierClient CreateClient(
        TravelOffer offer,
        bool timeoutOnReserve = false,
        bool declineOnReserve = false)
        => CreateClient(timeoutOnReserve, declineOnReserve, offer);

    private static SimulatedSupplierClient CreateClient(
        TravelOffer first,
        TravelOffer second,
        bool timeoutOnReserve = false,
        bool declineOnReserve = false)
        => CreateClient(timeoutOnReserve, declineOnReserve, first, second);

    private static SimulatedSupplierClient CreateClient(
        bool timeoutOnReserve,
        bool declineOnReserve,
        params TravelOffer[] offers)
    {
        var faults = new SupplierFaultHooks
        {
            TimeoutOnReserve = timeoutOnReserve,
            DeclineOnReserve = declineOnReserve,
        };
        return new SimulatedSupplierClient(new FakeOffers(offers), faults, new FixedClock(AsOf));
    }

    private static ReservationRequest Request(OfferId offerId) =>
        new(offerId, StayDate, "saga-1:ReserveInventory");

    private static TravelOffer Offer() =>
        TravelOffer.Create(
            SupplierId.New(),
            "Coral Bay Resort",
            new Destination("MBJ", "Montego Bay"),
            Money.Of(100.00m, Currency.Usd),
            Money.Of(15.00m, Currency.Usd),
            [OfferTag.Beach, OfferTag.Family],
            starRating: 4,
            availableFrom: new DateOnly(2026, 1, 1),
            availableTo: new DateOnly(2026, 12, 31));

    private sealed class FakeOffers(params TravelOffer[] offers) : IOfferRepository
    {
        public Task<TravelOffer?> GetByIdAsync(OfferId id, CancellationToken cancellationToken) =>
            Task.FromResult(offers.SingleOrDefault(o => o.Id == id));

        public Task<IReadOnlyList<TravelOffer>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TravelOffer>>(offers);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
