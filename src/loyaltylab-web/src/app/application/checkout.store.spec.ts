import { TestBed } from '@angular/core/testing';

import { BOOKING_PORT, err, ok, PRICING_PORT, WALLET_PORT } from '../domain';
import { GetBalanceUseCase, GetBookingUseCase, StartBookingUseCase } from './booking.use-case';
import { CheckoutStore } from './checkout.store';
import { ExplainQuoteUseCase } from './pricing.use-case';
import { clampedExplanation, compensatedBooking } from './test-fixtures';

describe('CheckoutStore', () => {
  it('clamps the tender to maxCredits from the quote', async () => {
    TestBed.configureTestingModule({
      providers: [
        CheckoutStore,
        ExplainQuoteUseCase,
        GetBalanceUseCase,
        StartBookingUseCase,
        GetBookingUseCase,
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
        { provide: BOOKING_PORT, useValue: { create: async () => ok(compensatedBooking) } },
      ],
    });

    const store = TestBed.inject(CheckoutStore);
    await store.load('q1');
    store.setCredits(50_000);

    expect(store.maxCredits()).toBe(4830);
    expect(store.credits()).toBe(4830);
  });

  it('reuses one idempotency key across retries of the same attempt', async () => {
    const keys: string[] = [];
    TestBed.configureTestingModule({
      providers: [
        CheckoutStore,
        ExplainQuoteUseCase,
        GetBalanceUseCase,
        StartBookingUseCase,
        GetBookingUseCase,
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
            create: async (_request: unknown, key: string) => {
              keys.push(key);
              return err({
                errorCode: 'TEMPORARY_FAILURE',
                message: 'blip',
                status: 503,
                correlationId: 'c1',
              });
            },
          },
        },
      ],
    });

    const store = TestBed.inject(CheckoutStore);
    await store.load('q1');
    await store.submit();
    await store.submit();

    expect(keys).toHaveLength(2);
    expect(keys[0]).toBe(keys[1]);
  });
});
