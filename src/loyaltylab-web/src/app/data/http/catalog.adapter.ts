import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { CatalogPort, OfferSummary, Result } from '../../domain';
import type { OfferDto } from '../dto/catalog.dto';
import { toOfferSummary } from '../mappers/catalog.mapper';
import { HttpResult } from './http-result';

@Injectable()
export class HttpCatalogAdapter implements CatalogPort {
  private readonly http = inject(HttpClient);
  private readonly results = inject(HttpResult);

  search(stayDate?: string): Promise<Result<readonly OfferSummary[]>> {
    const query = stayDate === undefined ? '' : `?stayDate=${encodeURIComponent(stayDate)}`;
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.get<readonly OfferDto[]>(`/api/offers${query}`));
      return dto.map(toOfferSummary);
    });
  }
}
