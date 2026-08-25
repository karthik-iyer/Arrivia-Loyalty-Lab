import { toPriceExplanationView, toQuoteView } from './pricing.mapper';
import type { ExplainDto, QuoteDto } from '../dto/pricing.dto';

/** Captured POST /api/offers/{id}/quote for Maya / Coral Bay. */
const quoteJson = `{
  "quoteId": "a11ce001-0006-7000-8000-000000000001",
  "offerId": "a11ce001-0004-7000-8000-000000000001",
  "memberPrice": { "amount": 120.75, "currency": "USD" },
  "maxCreditTender": { "amount": 48.3, "currency": "USD" },
  "maxCredits": 4830,
  "expiresAt": "2026-08-23T18:15:00+00:00"
}`;

/** Captured GET /api/quotes/{id}/explain for the quoting member — netCost/margin omitted. */
const memberExplainJson = `{
  "stages": [
    {
      "stage": "BaseMarkup",
      "order": 3,
      "description": "Partner markup 12%",
      "appliedRule": "a11ce001-0005-7000-8000-000000000001",
      "subtotalBefore": { "amount": 0, "currency": "USD" },
      "subtotalAfter": { "amount": 13.8, "currency": "USD" },
      "wasClamped": false
    },
    {
      "stage": "MarginFloor",
      "order": 6,
      "description": "Partner minimum margin",
      "appliedRule": "a11ce001-0005-7000-8000-000000000004",
      "subtotalBefore": { "amount": 4.83, "currency": "USD" },
      "subtotalAfter": { "amount": 5.75, "currency": "USD" },
      "wasClamped": true,
      "clampReason": "Raised by 0.92 to meet the partner minimum."
    }
  ],
  "memberPrice": { "amount": 120.75, "currency": "USD" },
  "maxCreditTender": { "amount": 48.3, "currency": "USD" }
}`;

describe('pricing mapper', () => {
  it('maps the worked-example quote and the raw JSON has no netRate', () => {
    expect(quoteJson).not.toMatch(/netRate/i);

    const view = toQuoteView(JSON.parse(quoteJson) as QuoteDto);

    expect(view.memberPrice).toEqual({ amount: 120.75, currency: 'USD' });
    expect(view.maxCreditTender).toEqual({ amount: 48.3, currency: 'USD' });
    expect(view.maxCredits).toBe(4830);
  });

  it('maps a member explanation with absent netCost and a clamped stage', () => {
    expect(memberExplainJson).not.toMatch(/netRate/i);
    expect(memberExplainJson).not.toContain('"netCost"');
    expect(memberExplainJson).not.toContain('"margin"');

    const view = toPriceExplanationView(JSON.parse(memberExplainJson) as ExplainDto);
    const floor = view.stages.find((stage) => stage.stage === 'MarginFloor');

    expect(view.netCost).toBeNull();
    expect(view.margin).toBeNull();
    expect(floor?.wasClamped).toBe(true);
    expect(floor?.clampReason).toContain('partner minimum');
  });
});
