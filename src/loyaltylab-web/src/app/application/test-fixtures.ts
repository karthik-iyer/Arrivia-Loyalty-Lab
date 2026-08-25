import type { OfferSummary, PriceExplanationView } from '../domain';

export const coralOffer: OfferSummary = {
  offerId: 'a11ce001-0004-7000-8000-000000000001',
  propertyName: 'Coral Bay Resort',
  destination: 'Montego Bay',
  destinationCode: 'MBJ',
  starRating: 4,
  tags: ['Beach', 'Family'],
  availableFrom: '2026-01-01',
  availableTo: '2026-06-30',
  memberPrice: { amount: 120.75, currency: 'USD' },
};

export const anonymousCoral: OfferSummary = { ...coralOffer, memberPrice: null };

export const clampedExplanation: PriceExplanationView = {
  stages: [
    {
      stage: 'BaseMarkup',
      order: 3,
      description: 'Partner markup 12%',
      appliedRule: null,
      subtotalBefore: { amount: 0, currency: 'USD' },
      subtotalAfter: { amount: 13.8, currency: 'USD' },
      wasClamped: false,
      clampReason: null,
    },
    {
      stage: 'MarginFloor',
      order: 6,
      description: 'Partner minimum margin',
      appliedRule: null,
      subtotalBefore: { amount: 4.83, currency: 'USD' },
      subtotalAfter: { amount: 5.75, currency: 'USD' },
      wasClamped: true,
      clampReason: 'Raised by 0.92 to meet the partner minimum.',
    },
  ],
  memberPrice: { amount: 120.75, currency: 'USD' },
  maxCreditTender: { amount: 48.3, currency: 'USD' },
  netCost: null,
  margin: null,
};
