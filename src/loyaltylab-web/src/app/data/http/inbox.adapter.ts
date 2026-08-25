import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { InboxPort, NudgeView, Result } from '../../domain';
import type { NudgeDto } from '../dto/inbox.dto';
import { toNudgeView } from '../mappers/inbox.mapper';
import { HttpResult } from './http-result';

@Injectable()
export class HttpInboxAdapter implements InboxPort {
  private readonly http = inject(HttpClient);
  private readonly results = inject(HttpResult);

  list(): Promise<Result<readonly NudgeView[]>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.get<readonly NudgeDto[]>('/api/inbox'));
      return dto.map(toNudgeView);
    });
  }

  action(nudgeId: string): Promise<Result<NudgeView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.post<NudgeDto>(`/api/inbox/${nudgeId}/action`, {}));
      return toNudgeView(dto);
    });
  }

  dismiss(nudgeId: string): Promise<Result<void>> {
    return this.results.capture(async () => {
      await firstValueFrom(this.http.post<void>(`/api/inbox/${nudgeId}/dismiss`, {}));
    });
  }
}
