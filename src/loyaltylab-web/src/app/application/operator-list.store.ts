import { computed, inject, Injectable, signal } from '@angular/core';

import type { AppError, SagaListItemView, SagaStatus } from '../domain';
import { ListSagasUseCase } from './operator.use-case';

export type OperatorListStatus = 'idle' | 'loading' | 'ready' | 'error';
export type SagaStatusFilter = 'all' | SagaStatus;

const STATUS_RANK: Record<SagaStatus, number> = {
  RequiresManualReview: 0,
  Running: 1,
  Compensating: 2,
  Compensated: 3,
  Confirmed: 4,
};

export function reviewNeededFirst(
  left: SagaListItemView,
  right: SagaListItemView,
): number {
  const byStatus = STATUS_RANK[left.status] - STATUS_RANK[right.status];
  if (byStatus !== 0) {
    return byStatus;
  }

  return right.startedAt.localeCompare(left.startedAt);
}

@Injectable()
export class OperatorListStore {
  private readonly listSagas = inject(ListSagasUseCase);

  private readonly _sagas = signal<readonly SagaListItemView[]>([]);
  private readonly _status = signal<OperatorListStatus>('idle');
  private readonly _error = signal<AppError | null>(null);
  private readonly _filter = signal<SagaStatusFilter>('all');

  readonly sagas = this._sagas.asReadonly();
  readonly status = this._status.asReadonly();
  readonly error = this._error.asReadonly();
  readonly filter = this._filter.asReadonly();

  readonly visible = computed(() => {
    const filter = this._filter();
    const rows = this._sagas().filter((saga) => filter === 'all' || saga.status === filter);
    return [...rows].sort(reviewNeededFirst);
  });

  setFilter(filter: SagaStatusFilter): void {
    this._filter.set(filter);
  }

  async load(): Promise<void> {
    this._status.set('loading');
    this._error.set(null);
    const result = await this.listSagas.execute();
    if (!result.ok) {
      this._error.set(result.error);
      this._status.set('error');
      return;
    }

    this._sagas.set(result.value);
    this._status.set('ready');
  }
}
