import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { PartnerPort, PartnerThemeView, Result } from '../../domain';
import type { PartnerThemeDto } from '../dto/partner.dto';
import { toPartnerThemeView } from '../mappers/partner.mapper';
import { HttpResult } from './http-result';

@Injectable()
export class HttpPartnerAdapter implements PartnerPort {
  private readonly http = inject(HttpClient);
  private readonly results = inject(HttpResult);

  theme(): Promise<Result<PartnerThemeView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.get<PartnerThemeDto>('/api/partners/current/theme'));
      return toPartnerThemeView(dto);
    });
  }
}
