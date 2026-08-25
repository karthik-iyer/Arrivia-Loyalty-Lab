import { TestBed } from '@angular/core/testing';

import { CATALOG_PORT, ok, PRICING_PORT } from '../domain';
import { OfferDetailStore } from './offer-detail.store';
import { ExplainQuoteUseCase, QuoteOfferUseCase } from './pricing.use-case';
import { SearchOffersUseCase } from './search-offers.use-case';
import { anonymousCoral, clampedExplanation, coralOffer } from './test-fixtures';

describe('OfferDetailStore', () => {
  it('quotes and explains when the member price is present', async () => {
    TestBed.configureTestingModule({
      providers: [
        OfferDetailStore,
        SearchOffersUseCase,
        QuoteOfferUseCase,
        ExplainQuoteUseCase,
        { provide: CATALOG_PORT, useValue: { search: async () => ok([coralOffer]) } },
        {
          provide: PRICING_PORT,
          useValue: {
            quote: async () =>
              ok({
                quoteId: 'q1',
                offerId: coralOffer.offerId,
                memberPrice: { amount: 120.75, currency: 'USD' },
                maxCreditTender: { amount: 48.3, currency: 'USD' },
                maxCredits: 4830,
                expiresAt: '2026-03-15T12:15:00Z',
              }),
            explain: async () => ok(clampedExplanation),
          },
        },
      ],
    });

    const store = TestBed.inject(OfferDetailStore);
    await store.load(coralOffer.offerId);

    expect(store.explanation()?.stages.some((stage) => stage.wasClamped)).toBe(true);
    expect(store.explanation()?.netCost).toBeNull();
  });

  it('does not quote when signed out', async () => {
    let quoted = false;
    TestBed.configureTestingModule({
      providers: [
        OfferDetailStore,
        SearchOffersUseCase,
        QuoteOfferUseCase,
        ExplainQuoteUseCase,
        { provide: CATALOG_PORT, useValue: { search: async () => ok([anonymousCoral]) } },
        {
          provide: PRICING_PORT,
          useValue: {
            quote: async () => {
              quoted = true;
              return ok({
                quoteId: 'q1',
                offerId: anonymousCoral.offerId,
                memberPrice: { amount: 1, currency: 'USD' },
                maxCreditTender: { amount: 1, currency: 'USD' },
                maxCredits: 1,
                expiresAt: '2026-03-15T12:15:00Z',
              });
            },
            explain: async () => ok(clampedExplanation),
          },
        },
      ],
    });

    const store = TestBed.inject(OfferDetailStore);
    await store.load(anonymousCoral.offerId);

    expect(quoted).toBe(false);
    expect(store.quote()).toBeNull();
    expect(store.status()).toBe('ready');
  });
});
