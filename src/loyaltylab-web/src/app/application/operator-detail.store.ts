import { computed, inject, Injectable, signal } from '@angular/core';

import type { AppError, SagaOperatorView } from '../domain';
import { GetSagaUseCase } from './operator.use-case';

export type OperatorDetailStatus = 'idle' | 'loading' | 'ready' | 'error';

@Injectable()
export class OperatorDetailStore {
  private readonly getSaga = inject(GetSagaUseCase);

  private readonly _detail = signal<SagaOperatorView | null>(null);
  private readonly _status = signal<OperatorDetailStatus>('idle');
  private readonly _error = signal<AppError | null>(null);

  readonly detail = this._detail.asReadonly();
  readonly status = this._status.asReadonly();
  readonly error = this._error.asReadonly();
  readonly needsReview = computed(() => this._detail()?.status === 'RequiresManualReview');

  async load(sagaId: string): Promise<void> {
    this._status.set('loading');
    this._error.set(null);
    const result = await this.getSaga.execute(sagaId);
    if (!result.ok) {
      this._error.set(result.error);
      this._status.set('error');
      return;
    }

    this._detail.set(result.value);
    this._status.set('ready');
  }
}
