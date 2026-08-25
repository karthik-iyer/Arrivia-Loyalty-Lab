import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { provideRouter } from '@angular/router';

import { BOOKING_PORT, ok, PRICING_PORT, WALLET_PORT } from '../../domain';
import { clampedExplanation, compensatedBooking } from '../../application/test-fixtures';
import { CheckoutPage } from './checkout-page';

describe('CheckoutPage', () => {
  it('states nothing was charged after a compensated saga', async () => {
    TestBed.configureTestingModule({
      imports: [CheckoutPage],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'q1' } } } },
        { provide: PRICING_PORT, useValue: { explain: async () => ok(clampedExplanation) } },
        {
          provide: WALLET_PORT,
          useValue: {
            balance: async () =>
              ok({
                memberId: 'maya',
                credits: 6000,
                monetaryValue: { amount: 60, currency: 'USD' },
                burnCap: 40,
              }),
          },
        },
        { provide: BOOKING_PORT, useValue: { create: async () => ok(compensatedBooking), get: async () => ok(compensatedBooking) } },
      ],
    });

    const fixture = TestBed.createComponent(CheckoutPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    await fixture.componentInstance.store.submit();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Nothing was charged. Your credits are unchanged.',
    );
    expect(fixture.nativeElement.querySelector('.step--compensated')).not.toBeNull();
  });
});
