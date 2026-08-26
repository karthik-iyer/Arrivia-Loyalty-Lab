import { computed, inject, Injectable, signal } from '@angular/core';

import type { AppError, AdminWorkerView, SagaListItemView, SagaStatus } from '../domain';
import { ListSagasUseCase, RunAdminWorkerUseCase } from './operator.use-case';

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
  private readonly runWorker = inject(RunAdminWorkerUseCase);

  private readonly _sagas = signal<readonly SagaListItemView[]>([]);
  private readonly _status = signal<OperatorListStatus>('idle');
  private readonly _error = signal<AppError | null>(null);
  private readonly _filter = signal<SagaStatusFilter>('all');
  private readonly _scanBusy = signal(false);
  private readonly _scanResult = signal<AdminWorkerView | null>(null);
  private readonly _scanError = signal<AppError | null>(null);

  readonly sagas = this._sagas.asReadonly();
  readonly status = this._status.asReadonly();
  readonly error = this._error.asReadonly();
  readonly filter = this._filter.asReadonly();
  readonly scanBusy = this._scanBusy.asReadonly();
  readonly scanResult = this._scanResult.asReadonly();
  readonly scanError = this._scanError.asReadonly();

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

  async runScan(): Promise<void> {
    if (this._scanBusy()) {
      return;
    }

    this._scanBusy.set(true);
    this._scanError.set(null);
    this._scanResult.set(null);
    const result = await this.runWorker.execute('scan');
    this._scanBusy.set(false);
    if (!result.ok) {
      this._scanError.set(result.error);
      return;
    }

    this._scanResult.set(result.value);
  }
}
