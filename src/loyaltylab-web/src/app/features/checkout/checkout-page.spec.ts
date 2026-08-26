import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { provideRouter } from '@angular/router';

import { BOOKING_PORT, ok, PRICING_PORT, WALLET_PORT } from '../../domain';
import { cancelledBooking, clampedExplanation, compensatedBooking, confirmedBooking } from '../../application/test-fixtures';
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

  it('cancels a confirmed booking and offers the wallet', async () => {
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
        {
          provide: BOOKING_PORT,
          useValue: {
            create: async () => ok(confirmedBooking),
            get: async () => ok(confirmedBooking),
            cancel: async () => ok(cancelledBooking),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(CheckoutPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    await fixture.componentInstance.store.submit();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Booking confirmed.');

    const cancel = [...(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)].find(
      (button) => button.textContent?.includes('Cancel booking'),
    );
    cancel?.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Booking cancelled. Your credits are restored.');
    expect(fixture.nativeElement.textContent).toContain('Open wallet');
  });
});
