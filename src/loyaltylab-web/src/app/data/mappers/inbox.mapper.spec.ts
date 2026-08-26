import { toActionedQuote, toInboxNudges } from './inbox.mapper';
import type { ActionedNudgeDto, InboxDto } from '../dto/inbox.dto';

/** Captured-shape GET /api/inbox after a scan for Maya (docs/04 §10). */
const inboxJson = `{
  "nudges": [
    {
      "nudgeId": "a11ce001-000e-7000-8000-000000000001",
      "offerId": "a11ce001-0004-7000-8000-000000000001",
      "propertyName": "Coral Bay Resort",
      "windowStart": "2026-03-29",
      "windowEnd": "2026-04-12",
      "score": 0.68,
      "expiresAt": "2026-03-22T12:00:00+00:00",
      "signals": [
        { "kind": "WindowFit", "rawValue": 14, "normalized": 1, "weight": 0.2, "contribution": 0.2 },
        { "kind": "DestinationAffinity", "rawValue": 3, "normalized": 1, "weight": 0.2, "contribution": 0.2 },
        { "kind": "TagAffinity", "rawValue": 0.4, "normalized": 0.4, "weight": 0.2, "contribution": 0.08 },
        { "kind": "CreditCoverage", "rawValue": 0.4, "normalized": 0.4, "weight": 0.2, "contribution": 0.08 },
        { "kind": "PriceDrop", "rawValue": 0.13, "normalized": 0.43, "weight": 0.2, "contribution": 0.086 }
      ]
    }
  ]
}`;

const actionJson = `{
  "nudgeId": "a11ce001-000e-7000-8000-000000000001",
  "quoteId": "a11ce001-000b-7000-8000-000000000099",
  "offerId": "a11ce001-0004-7000-8000-000000000001",
  "memberPrice": { "amount": 120.75, "currency": "USD" },
  "maxCreditTender": { "amount": 48.30, "currency": "USD" },
  "maxCredits": 4830,
  "expiresAt": "2026-03-15T12:15:00+00:00"
}`;

describe('inbox mapper', () => {
  it('maps property name, window, and signal weights', () => {
    const nudges = toInboxNudges(JSON.parse(inboxJson) as InboxDto);
    const nudge = nudges[0];

    expect(nudge?.propertyName).toBe('Coral Bay Resort');
    expect(nudge?.windowStart).toBe('2026-03-29');
    expect(nudge?.signals).toHaveLength(5);
    expect(nudge?.signals[0]?.kind).toBe('WindowFit');
    expect(nudge?.signals[0]?.weight).toBe(0.2);
  });

  it('maps actioning to a live quote, not a stored price', () => {
    const quote = toActionedQuote(JSON.parse(actionJson) as ActionedNudgeDto);

    expect(quote.quoteId).toBe('a11ce001-000b-7000-8000-000000000099');
    expect(quote.memberPrice.amount).toBe(120.75);
    expect(quote.maxCredits).toBe(4830);
  });
});
