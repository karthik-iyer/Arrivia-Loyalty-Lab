import { inject, Injectable } from '@angular/core';

import { CATALOG_PORT, type OfferSummary, type Result } from '../domain';

@Injectable({ providedIn: 'root' })
export class SearchOffersUseCase {
  private readonly catalog = inject(CATALOG_PORT);

  execute(stayDate?: string): Promise<Result<readonly OfferSummary[]>> {
    return this.catalog.search(stayDate);
  }
}
