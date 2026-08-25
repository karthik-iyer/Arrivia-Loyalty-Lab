import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';

import type { AppError, BookingView, Money, PriceExplanationView } from '../domain';
import { GetBalanceUseCase, GetBookingUseCase, StartBookingUseCase } from './booking.use-case';
import { DEMO_STAY_DATE } from './demo-stay';
import { ExplainQuoteUseCase } from './pricing.use-case';

export type CheckoutStatus = 'idle' | 'loading' | 'ready' | 'settling' | 'done' | 'failed';
export type SagaOutcome = 'pending' | 'confirmed' | 'unwound' | 'needs-review';

const fastPollMs = 500;
const slowPollMs = 2000;
const fastPollCount = 6;

@Injectable()
export class CheckoutStore {
  private readonly explainQuote = inject(ExplainQuoteUseCase);
  private readonly getBalance = inject(GetBalanceUseCase);
  private readonly startBooking = inject(StartBookingUseCase);
  private readonly getBooking = inject(GetBookingUseCase);

  private quoteId = '';
  private idempotencyKey: string | null = null;
  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private pollTicks = 0;

  private readonly _explanation = signal<PriceExplanationView | null>(null);
  private readonly _walletCredits = signal(0);
  private readonly _credits = signal(0);
  private readonly _forceDecline = signal(false);
  private readonly _booking = signal<BookingView | null>(null);
  private readonly _status = signal<CheckoutStatus>('idle');
  private readonly _error = signal<AppError | null>(null);

  readonly explanation = this._explanation.asReadonly();
  readonly credits = this._credits.asReadonly();
  readonly forceDecline = this._forceDecline.asReadonly();
  readonly booking = this._booking.asReadonly();
  readonly status = this._status.asReadonly();
  readonly error = this._error.asReadonly();
  readonly steps = computed(() => this._booking()?.saga.steps ?? []);
  readonly isSettling = computed(() => this._status() === 'settling');
  readonly outcome = computed<SagaOutcome>(() => {
    const sagaStatus = this._booking()?.saga.status;
    if (sagaStatus === 'Confirmed') {
      return 'confirmed';
    }
    if (sagaStatus === 'Compensated') {
      return 'unwound';
    }
    if (sagaStatus === 'RequiresManualReview') {
      return 'needs-review';
    }
    return 'pending';
  });

  readonly memberPrice = computed(() => this._explanation()?.memberPrice ?? null);

  readonly maxCredits = computed(() => {
    const tender = this._explanation()?.maxCreditTender;
    const fromQuote = tender == null ? 0 : Math.round(tender.amount * 100);
    return Math.min(fromQuote, this._walletCredits());
  });

  readonly cash = computed<Money | null>(() => {
    const price = this.memberPrice();
    if (!price) {
      return null;
    }

    const max = this.maxCredits();
    const tender = this._explanation()?.maxCreditTender;
    const rate = max > 0 && tender ? tender.amount / max : 0.01;
    const amount = Math.round((price.amount - this._credits() * rate) * 100) / 100;
    return { amount, currency: price.currency };
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stopPolling());
  }

  async load(quoteId: string): Promise<void> {
    this.quoteId = quoteId;
    this._status.set('loading');
    this._error.set(null);

    const explained = await this.explainQuote.execute(quoteId);
    if (!explained.ok) {
      this._error.set(explained.error);
      this._status.set('failed');
      return;
    }

    const balance = await this.getBalance.execute();
    if (!balance.ok) {
      this._error.set(balance.error);
      this._status.set('failed');
      return;
    }

    this._explanation.set(explained.value);
    this._walletCredits.set(balance.value.credits);
    const cap = this.maxCredits();
    this._credits.set(cap);
    this._status.set('ready');
  }

  setCredits(value: number): void {
    const next = Math.min(this.maxCredits(), Math.max(0, Math.round(value)));
    if (next !== this._credits()) {
      this.idempotencyKey = null;
    }

    this._credits.set(next);
  }

  setForceDecline(value: boolean): void {
    this._forceDecline.set(value);
    this.idempotencyKey = null;
  }

  async submit(): Promise<void> {
    if (this._status() === 'settling') {
      return;
    }

    this.idempotencyKey ??= crypto.randomUUID();
    this._status.set('settling');
    this._error.set(null);

    const result = await this.startBooking.execute(
      { quoteId: this.quoteId, credits: this._credits(), stayDate: DEMO_STAY_DATE },
      this.idempotencyKey,
      { forcePaymentDecline: this._forceDecline() },
    );

    if (!result.ok) {
      this._error.set(result.error);
      this._status.set('failed');
      return;
    }

    this._booking.set(result.value);
    if (result.value.saga.status === 'Running' || result.value.saga.status === 'Compensating') {
      this.beginPolling(result.value.bookingId);
      return;
    }

    this._status.set('done');
  }

  private beginPolling(bookingId: string): void {
    this.stopPolling();
    this.pollTicks = 0;
    this.schedulePoll(bookingId);
  }

  private schedulePoll(bookingId: string): void {
    const delay = this.pollTicks < fastPollCount ? fastPollMs : slowPollMs;
    this.pollTimer = setTimeout(() => {
      void this.pollOnce(bookingId);
    }, delay);
  }

  private async pollOnce(bookingId: string): Promise<void> {
    this.pollTicks += 1;
    const result = await this.getBooking.execute(bookingId);
    if (!result.ok) {
      this._error.set(result.error);
      this._status.set('failed');
      this.stopPolling();
      return;
    }

    this._booking.set(result.value);
    const saga = result.value.saga.status;
    if (saga === 'Running' || saga === 'Compensating') {
      this.schedulePoll(bookingId);
      return;
    }

    this._status.set('done');
    this.stopPolling();
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearTimeout(this.pollTimer);
      this.pollTimer = null;
    }
  }
}
