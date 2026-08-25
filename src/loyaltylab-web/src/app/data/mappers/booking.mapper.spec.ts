import { toBookingView } from './booking.mapper';
import type { BookingDto } from '../dto/booking.dto';

/** Captured POST /api/bookings for a confirmed mixed-tender checkout (docs/04 §10). */
const confirmedBookingJson = `{
  "bookingId": "a11ce001-0007-7000-8000-000000000001",
  "status": "Confirmed",
  "tender": { "cash": { "amount": 72.45, "currency": "USD" }, "credits": 4830 },
  "drift": { "applied": "Absorbed", "netRateDelta": { "amount": 1.2, "currency": "USD" } },
  "saga": {
    "id": "a11ce001-0008-7000-8000-000000000001",
    "status": "Confirmed",
    "steps": [
      { "kind": "ValidateQuote", "status": "Succeeded", "attempts": 1, "durationMs": 12 },
      {
        "kind": "ReserveInventory",
        "status": "Succeeded",
        "attempts": 2,
        "externalReference": "OCE-88213",
        "durationMs": 340
      },
      { "kind": "AuthorizePayment", "status": "Succeeded", "attempts": 1, "durationMs": 180 },
      { "kind": "BurnCredits", "status": "Succeeded", "attempts": 1 },
      { "kind": "CapturePayment", "status": "Succeeded", "attempts": 1 },
      { "kind": "ConfirmBooking", "status": "Succeeded", "attempts": 1 }
    ]
  }
}`;

/** Captured operator saga after exhausted compensation (T-040). */
const reviewNeededJson = `{
  "bookingId": "a11ce001-0007-7000-8000-000000000002",
  "status": "Failed",
  "tender": { "cash": { "amount": 120.75, "currency": "USD" }, "credits": 0 },
  "saga": {
    "id": "a11ce001-0008-7000-8000-000000000002",
    "status": "RequiresManualReview",
    "steps": [
      { "kind": "ValidateQuote", "status": "Succeeded", "attempts": 1 },
      {
        "kind": "ReserveInventory",
        "status": "CompensationFailed",
        "attempts": 1,
        "externalReference": "OCE-99101",
        "errorCode": "SUPPLIER_UNAVAILABLE",
        "compensation": {
          "status": "Failed",
          "attempts": 5,
          "errorCode": "SUPPLIER_UNAVAILABLE"
        }
      }
    ]
  }
}`;

describe('booking mapper', () => {
  it('maps a confirmed saga with every step and absorbed drift', () => {
    const view = toBookingView(JSON.parse(confirmedBookingJson) as BookingDto);

    expect(view.status).toBe('Confirmed');
    expect(view.tender.credits).toBe(4830);
    expect(view.drift?.applied).toBe('Absorbed');
    expect(view.saga.steps).toHaveLength(6);
    expect(view.saga.steps.every((step) => step.status === 'Succeeded')).toBe(true);
    expect(view.saga.steps[1]?.externalReference).toBe('OCE-88213');
    expect(view.saga.steps.every((step) => step.compensation === null)).toBe(true);
  });

  it('maps compensation failure onto the step without inventing success', () => {
    const view = toBookingView(JSON.parse(reviewNeededJson) as BookingDto);
    const reserve = view.saga.steps[1];

    expect(view.saga.status).toBe('RequiresManualReview');
    expect(reserve?.status).toBe('CompensationFailed');
    expect(reserve?.error).toBe('SUPPLIER_UNAVAILABLE');
    expect(reserve?.compensation?.attempts).toBe(5);
    expect(reserve?.compensation?.status).toBe('Failed');
  });
});
