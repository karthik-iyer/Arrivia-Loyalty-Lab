import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { PriceExplanationView, PricingPort, QuoteOfferRequest, QuoteView, Result } from '../../domain';
import type { ExplainDto, QuoteDto } from '../dto/pricing.dto';
import { toPriceExplanationView, toQuoteView } from '../mappers/pricing.mapper';
import { HttpResult } from './http-result';

@Injectable()
export class HttpPricingAdapter implements PricingPort {
  private readonly http = inject(HttpClient);
  private readonly results = inject(HttpResult);

  quote(offerId: string, request?: QuoteOfferRequest): Promise<Result<QuoteView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(
        this.http.post<QuoteDto>(`/api/offers/${offerId}/quote`, request ?? {}),
      );
      return toQuoteView(dto);
    });
  }

  explain(quoteId: string): Promise<Result<PriceExplanationView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.get<ExplainDto>(`/api/quotes/${quoteId}/explain`));
      return toPriceExplanationView(dto);
    });
  }
}
