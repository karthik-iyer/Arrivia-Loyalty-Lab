using System.Collections.Concurrent;
using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Infrastructure.Suppliers;

internal enum ReservationStatus
{
    Held = 0,
    Released = 1,
    Declined = 2,
}

internal sealed class SupplierReservation
{
    public required string Id { get; init; }

    public required string IdempotencyKey { get; init; }

    public required OfferId OfferId { get; init; }

    public required DateOnly StayDate { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public ReservationStatus Status { get; set; }
}

/// <summary>
/// Insert-first by idempotency key so concurrent reserves produce one hold.
/// </summary>
internal sealed class SupplierReservationStore
{
    private readonly ConcurrentDictionary<string, SupplierReservation> _byKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SupplierReservation> _byId = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public SupplierReservation GetOrAdd(string key, Func<SupplierReservation> factory, out bool inserted)
    {
        lock (_gate)
        {
            if (_byKey.TryGetValue(key, out var existing))
            {
                inserted = false;
                return existing;
            }

            var created = factory();
            _byKey[key] = created;
            _byId[created.Id] = created;
            inserted = true;
            return created;
        }
    }

    public SupplierReservation? FindByKey(string key)
    {
        lock (_gate)
        {
            return _byKey.TryGetValue(key, out var reservation) ? reservation : null;
        }
    }

    public SupplierReservation? FindById(string id)
    {
        lock (_gate)
        {
            return _byId.TryGetValue(id, out var reservation) ? reservation : null;
        }
    }

    public void ReleaseHeld(SupplierReservation reservation)
    {
        lock (_gate)
        {
            if (reservation.Status == ReservationStatus.Held)
            {
                reservation.Status = ReservationStatus.Released;
            }
        }
    }
}

public sealed class SimulatedSupplierClient : ISupplierClient
{
    private readonly IOfferRepository _offers;
    private readonly SupplierReservationStore _store;
    private readonly SupplierFaultHooks _faults;
    private readonly IClock _clock;

    public SimulatedSupplierClient(IOfferRepository offers, SupplierFaultHooks faults, IClock clock)
        : this(offers, new SupplierReservationStore(), faults, clock)
    {
    }

    internal SimulatedSupplierClient(
        IOfferRepository offers,
        SupplierReservationStore store,
        SupplierFaultHooks faults,
        IClock clock)
    {
        _offers = offers;
        _store = store;
        _faults = faults;
        _clock = clock;
    }

    public async Task<Result<Money>> GetCurrentNetRateAsync(OfferId offerId, CancellationToken cancellationToken)
    {
        var offer = await _offers.GetByIdAsync(offerId, cancellationToken);
        return offer is null
            ? Result<Money>.Failure(Errors.OfferNotFound)
            : Result<Money>.Success(offer.NetRate);
    }

    public async Task<StepOutcome> ReserveAsync(ReservationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await DelayIfRequested(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return StepOutcome.Failed(Errors.SupplierUnavailable);
        }

        var key = request.IdempotencyKey.Trim();
        var offer = await _offers.GetByIdAsync(request.OfferId, cancellationToken);
        if (offer is null)
        {
            return StepOutcome.Failed(Errors.OfferNotFound);
        }

        var declined = _faults.DeclineOnReserve;
        var reservation = _store.GetOrAdd(
            key,
            () => new SupplierReservation
            {
                Id = Guid.CreateVersion7().ToString(),
                IdempotencyKey = key,
                OfferId = request.OfferId,
                StayDate = request.StayDate,
                CreatedAt = _clock.UtcNow,
                Status = declined ? ReservationStatus.Declined : ReservationStatus.Held,
            },
            out var inserted);

        if (!inserted)
        {
            return SamePayload(reservation, request)
                ? ToReserveReplay(reservation)
                : StepOutcome.Failed(Errors.IdempotencyKeyReused, reservation.Id);
        }

        if (reservation.Status == ReservationStatus.Declined)
        {
            return StepOutcome.Failed(Errors.SupplierUnavailable, reservation.Id);
        }

        // Store first, then report Unknown — the in-process analogue of PaymentSim's
        // hang-after-commit, so query-by-key can resolve FR-B-04.
        return _faults.TimeoutOnReserve
            ? StepOutcome.Unknown()
            : StepOutcome.Succeeded(reservation.Id);
    }

    public async Task<StepOutcome> ReleaseAsync(string reference, CancellationToken cancellationToken)
    {
        await DelayIfRequested(cancellationToken);
        if (_faults.FailOnRelease)
        {
            return StepOutcome.Failed(Errors.SupplierUnavailable);
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            return StepOutcome.Failed(Errors.SupplierUnavailable);
        }

        var reservation = _store.FindById(reference.Trim());
        if (reservation is null)
        {
            return StepOutcome.Failed(Errors.SupplierUnavailable);
        }

        _store.ReleaseHeld(reservation);
        return StepOutcome.Succeeded(reservation.Id);
    }

    public Task<StepOutcome> QueryReservationAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Task.FromResult(StepOutcome.Failed(Errors.SupplierUnavailable));
        }

        var reservation = _store.FindByKey(idempotencyKey.Trim());
        if (reservation is null || reservation.Status != ReservationStatus.Held)
        {
            return Task.FromResult(StepOutcome.Failed(Errors.SupplierUnavailable));
        }

        return Task.FromResult(StepOutcome.Succeeded(reservation.Id));
    }

    private static bool SamePayload(SupplierReservation reservation, ReservationRequest request) =>
        reservation.OfferId == request.OfferId && reservation.StayDate == request.StayDate;

    private static StepOutcome ToReserveReplay(SupplierReservation reservation) =>
        reservation.Status switch
        {
            ReservationStatus.Held => StepOutcome.Succeeded(reservation.Id),
            ReservationStatus.Declined => StepOutcome.Failed(Errors.SupplierUnavailable, reservation.Id),
            _ => StepOutcome.Failed(Errors.SupplierUnavailable, reservation.Id),
        };

    private Task DelayIfRequested(CancellationToken cancellationToken) =>
        _faults.AddedLatencyMs > 0
            ? Task.Delay(_faults.AddedLatencyMs, cancellationToken)
            : Task.CompletedTask;
}
