import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type {
  AdminWorkerName,
  AdminWorkerView,
  OperatorPort,
  Result,
  SagaListItemView,
  SagaOperatorView,
} from '../../domain';
import type { AdminWorkerDto, SagaListItemDto, SagaOperatorDto } from '../dto/operator.dto';
import { toAdminWorkerView, toSagaListItemView, toSagaOperatorView } from '../mappers/operator.mapper';
import { HttpResult } from './http-result';

@Injectable()
export class HttpOperatorAdapter implements OperatorPort {
  private readonly http = inject(HttpClient);
  private readonly results = inject(HttpResult);

  listSagas(): Promise<Result<readonly SagaListItemView[]>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.get<readonly SagaListItemDto[]>('/api/operator/sagas'));
      return dto.map(toSagaListItemView);
    });
  }

  getSaga(sagaId: string): Promise<Result<SagaOperatorView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.get<SagaOperatorDto>(`/api/operator/sagas/${sagaId}`));
      return toSagaOperatorView(dto);
    });
  }

  runWorker(worker: AdminWorkerName): Promise<Result<AdminWorkerView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.post<AdminWorkerDto>(`/api/admin/run/${worker}`, {}));
      return toAdminWorkerView(dto);
    });
  }
}
