import { toSagaOperatorView } from './operator.mapper';
import type { SagaOperatorDto } from '../dto/operator.dto';

/** Captured GET /api/operator/sagas/{id} after a confirmed checkout. */
const operatorJson = `{
  "id": "a11ce001-0008-7000-8000-000000000001",
  "bookingId": "a11ce001-0007-7000-8000-000000000001",
  "status": "Confirmed",
  "startedAt": "2026-03-15T12:00:00+00:00",
  "lastHeartbeatAt": "2026-03-15T12:00:02+00:00",
  "completedAt": "2026-03-15T12:00:02+00:00",
  "steps": [
    { "kind": "ValidateQuote", "status": "Succeeded", "attempts": 1 },
    { "kind": "ReserveInventory", "status": "Succeeded", "attempts": 1, "externalReference": "OCE-88213" },
    { "kind": "AuthorizePayment", "status": "Succeeded", "attempts": 1 },
    { "kind": "BurnCredits", "status": "Succeeded", "attempts": 1 },
    { "kind": "CapturePayment", "status": "Succeeded", "attempts": 1 },
    { "kind": "ConfirmBooking", "status": "Succeeded", "attempts": 1 }
  ],
  "poison": []
}`;

describe('operator mapper', () => {
  it('maps steps, attempts, and an empty poison list', () => {
    const view = toSagaOperatorView(JSON.parse(operatorJson) as SagaOperatorDto);

    expect(view.steps).toHaveLength(6);
    expect(view.steps.every((step) => step.attempts >= 1)).toBe(true);
    expect(view.poison).toEqual([]);
    expect(view.completedAt).not.toBeNull();
  });
});
