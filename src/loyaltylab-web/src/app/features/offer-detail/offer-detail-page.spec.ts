import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';

import { clampedExplanation, coralOffer } from '../../application/test-fixtures';
import { CATALOG_PORT, ok, PRICING_PORT } from '../../domain';
import { OfferDetailPage } from './offer-detail-page';

describe('OfferDetailPage', () => {
  async function render(): Promise<ComponentFixture<OfferDetailPage>> {
    await TestBed.configureTestingModule({
      imports: [OfferDetailPage],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => coralOffer.offerId } } } },
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
    }).compileComponents();

    const fixture = TestBed.createComponent(OfferDetailPage);
    fixture.detectChanges();
    await fixture.componentInstance.store.load(coralOffer.offerId);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('renders the quote and a checkout link from fake ports, not HTTP', async () => {
    const fixture = await render();
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Coral Bay Resort');
    expect(text).toContain('$120.75');
    expect(text).toContain('Continue to checkout');
  });
});
