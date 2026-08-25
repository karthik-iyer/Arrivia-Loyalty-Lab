import { toConciergeView } from './concierge.mapper';
import type { ConciergeDto } from '../dto/concierge.dto';

/** Captured-shape POST /api/concierge/recommend from docs/04 §10. */
const conciergeJson = `{
  "narrative": "Three beach stays fit your dates, and credits cover most of the first.",
  "narrationApplied": false,
  "recommendations": [
    {
      "offerId": "a11ce001-0004-7000-8000-000000000001",
      "propertyName": "Coral Bay Resort",
      "quoteId": "a11ce001-0006-7000-8000-000000000001",
      "memberPrice": { "amount": 120.75, "currency": "USD" },
      "creditsCover": 4830,
      "score": 0.82,
      "reasons": ["Strong value for money", "Credits cover 40%", "Matches: beach"]
    }
  ],
  "audit": {
    "candidatesConsidered": 24,
    "candidatesReturned": 3,
    "interpretedTerms": ["beach", "March"],
    "exclusions": [
      {
        "offerId": "a11ce001-0004-7000-8000-000000000003",
        "reason": "SupplierNotPermitted",
        "detail": "OCEANIC not permitted for NIMBUS"
      }
    ],
    "weights": {
      "valueForMoney": 0.4,
      "creditCoverage": 0.25,
      "tagMatch": 0.2,
      "starRating": 0.15
    },
    "narrationApplied": false
  }
}`;

describe('concierge mapper', () => {
  it('maps recommendations and every exclusion reason', () => {
    const view = toConciergeView(JSON.parse(conciergeJson) as ConciergeDto);

    expect(view.recommendations[0]?.memberPrice.amount).toBe(120.75);
    expect(view.audit.exclusions[0]?.reason).toBe('SupplierNotPermitted');
    expect(view.audit.interpretedTerms).toEqual(['beach', 'March']);
  });
});
