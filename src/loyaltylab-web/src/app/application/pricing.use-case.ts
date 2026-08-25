import { inject, Injectable } from '@angular/core';

import {
  PRICING_PORT,
  type PriceExplanationView,
  type QuoteOfferRequest,
  type QuoteView,
  type Result,
} from '../domain';

@Injectable({ providedIn: 'root' })
export class QuoteOfferUseCase {
  private readonly pricing = inject(PRICING_PORT);

  execute(offerId: string, request?: QuoteOfferRequest): Promise<Result<QuoteView>> {
    return this.pricing.quote(offerId, request);
  }
}

@Injectable({ providedIn: 'root' })
export class ExplainQuoteUseCase {
  private readonly pricing = inject(PRICING_PORT);

  execute(quoteId: string): Promise<Result<PriceExplanationView>> {
    return this.pricing.explain(quoteId);
  }
}
