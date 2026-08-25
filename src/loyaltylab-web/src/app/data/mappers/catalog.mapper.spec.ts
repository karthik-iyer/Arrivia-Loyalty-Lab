import { toOfferSummary } from './catalog.mapper';
import type { OfferDto } from '../dto/catalog.dto';

/** Captured GET /api/offers for anonymous SUMMIT — memberPrice omitted (WhenWritingNull). */
const anonymousCatalogJson = `[
  {
    "offerId": "a11ce001-0004-7000-8000-000000000001",
    "propertyName": "Coral Bay Resort",
    "destinationCode": "MBJ",
    "destinationName": "Montego Bay",
    "starRating": 4,
    "tags": ["Beach", "Family"],
    "availableFrom": "2026-01-01",
    "availableTo": "2026-06-30"
  }
]`;

/** Captured GET /api/offers for Maya — memberPrice present, netRate still absent. */
const memberCatalogJson = `[
  {
    "offerId": "a11ce001-0004-7000-8000-000000000001",
    "propertyName": "Coral Bay Resort",
    "destinationCode": "MBJ",
    "destinationName": "Montego Bay",
    "starRating": 4,
    "tags": ["Beach", "Family"],
    "availableFrom": "2026-01-01",
    "availableTo": "2026-06-30",
    "memberPrice": { "amount": 120.75, "currency": "USD" }
  }
]`;

describe('catalog mapper', () => {
  it('maps an anonymous payload to a null memberPrice and the raw JSON has no netRate', () => {
    expect(anonymousCatalogJson).not.toMatch(/netRate/i);
    expect(anonymousCatalogJson).not.toContain('memberPrice');

    const [dto] = JSON.parse(anonymousCatalogJson) as OfferDto[];
    const offer = toOfferSummary(dto);

    expect(offer.propertyName).toBe('Coral Bay Resort');
    expect(offer.destination).toBe('Montego Bay');
    expect(offer.memberPrice).toBeNull();
  });

  it('maps a member payload including the worked-example price without leaking netRate', () => {
    expect(memberCatalogJson).not.toMatch(/netRate/i);

    const [dto] = JSON.parse(memberCatalogJson) as OfferDto[];
    const offer = toOfferSummary(dto);

    expect(offer.memberPrice).toEqual({ amount: 120.75, currency: 'USD' });
  });
});
