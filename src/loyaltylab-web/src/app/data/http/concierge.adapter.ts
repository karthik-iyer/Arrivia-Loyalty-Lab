import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { ConciergePort, ConciergeRequest, ConciergeView, Result } from '../../domain';
import type { ConciergeDto } from '../dto/concierge.dto';
import { toConciergeRequestDto, toConciergeView } from '../mappers/concierge.mapper';
import { HttpResult } from './http-result';

@Injectable()
export class HttpConciergeAdapter implements ConciergePort {
  private readonly http = inject(HttpClient);
  private readonly results = inject(HttpResult);

  recommend(request: ConciergeRequest): Promise<Result<ConciergeView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(
        this.http.post<ConciergeDto>('/api/concierge/recommend', toConciergeRequestDto(request)),
      );
      return toConciergeView(dto);
    });
  }
}
