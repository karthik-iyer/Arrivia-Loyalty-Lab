import { inject, Injectable } from '@angular/core';

import {
  OPERATOR_PORT,
  type Result,
  type SagaListItemView,
  type SagaOperatorView,
} from '../domain';

@Injectable({ providedIn: 'root' })
export class ListSagasUseCase {
  private readonly operator = inject(OPERATOR_PORT);

  execute(): Promise<Result<readonly SagaListItemView[]>> {
    return this.operator.listSagas();
  }
}

@Injectable({ providedIn: 'root' })
export class GetSagaUseCase {
  private readonly operator = inject(OPERATOR_PORT);

  execute(sagaId: string): Promise<Result<SagaOperatorView>> {
    return this.operator.getSaga(sagaId);
  }
}
