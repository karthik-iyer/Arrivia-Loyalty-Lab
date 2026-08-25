import { inject, Injectable, signal } from '@angular/core';

import type { AppError, OfferSummary, PriceExplanationView, QuoteView } from '../domain';
import { DEMO_STAY_DATE } from './demo-stay';
import { ExplainQuoteUseCase, QuoteOfferUseCase } from './pricing.use-case';
import { SearchOffersUseCase } from './search-offers.use-case';

export type OfferDetailStatus = 'idle' | 'loading' | 'ready' | 'error';

@Injectable()
export class OfferDetailStore {
  private readonly searchOffers = inject(SearchOffersUseCase);
  private readonly quoteOffer = inject(QuoteOfferUseCase);
  private readonly explainQuote = inject(ExplainQuoteUseCase);

  private readonly _offer = signal<OfferSummary | null>(null);
  private readonly _quote = signal<QuoteView | null>(null);
  private readonly _explanation = signal<PriceExplanationView | null>(null);
  private readonly _status = signal<OfferDetailStatus>('idle');
  private readonly _error = signal<AppError | null>(null);

  readonly offer = this._offer.asReadonly();
  readonly quote = this._quote.asReadonly();
  readonly explanation = this._explanation.asReadonly();
  readonly status = this._status.asReadonly();
  readonly error = this._error.asReadonly();

  async load(offerId: string, stayDate = DEMO_STAY_DATE): Promise<void> {
    this._status.set('loading');
    this._error.set(null);
    this._quote.set(null);
    this._explanation.set(null);

    const catalog = await this.searchOffers.execute(stayDate);
    if (!catalog.ok) {
      this._error.set(catalog.error);
      this._status.set('error');
      return;
    }

    const offer = catalog.value.find((item) => item.offerId === offerId) ?? null;
    this._offer.set(offer);
    if (!offer) {
      this._error.set({
        errorCode: 'OFFER_NOT_FOUND',
        message: 'The offer was not found.',
        status: 404,
        correlationId: null,
      });
      this._status.set('error');
      return;
    }

    if (offer.memberPrice === null) {
      this._status.set('ready');
      return;
    }

    const quoted = await this.quoteOffer.execute(offerId, { stayDate });
    if (!quoted.ok) {
      this._error.set(quoted.error);
      this._status.set('error');
      return;
    }

    this._quote.set(quoted.value);
    const explained = await this.explainQuote.execute(quoted.value.quoteId);
    if (!explained.ok) {
      this._error.set(explained.error);
      this._status.set('error');
      return;
    }

    this._explanation.set(explained.value);
    this._status.set('ready');
  }
}
