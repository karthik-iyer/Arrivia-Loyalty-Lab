import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { InboxPort, NudgeView, QuoteView, Result } from '../../domain';
import type { ActionedNudgeDto, InboxDto } from '../dto/inbox.dto';
import { toActionedQuote, toInboxNudges } from '../mappers/inbox.mapper';
import { HttpResult } from './http-result';

@Injectable()
export class HttpInboxAdapter implements InboxPort {
  private readonly http = inject(HttpClient);
  private readonly results = inject(HttpResult);

  list(): Promise<Result<readonly NudgeView[]>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.get<InboxDto>('/api/inbox'));
      return toInboxNudges(dto);
    });
  }

  action(nudgeId: string): Promise<Result<QuoteView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(
        this.http.post<ActionedNudgeDto>(`/api/inbox/${nudgeId}/action`, {}),
      );
      return toActionedQuote(dto);
    });
  }

  dismiss(nudgeId: string): Promise<Result<void>> {
    return this.results.capture(async () => {
      await firstValueFrom(this.http.post(`/api/inbox/${nudgeId}/dismiss`, {}));
    });
  }
}
