import { inject, Injectable, signal } from '@angular/core';

import type { AppError, ConciergeView } from '../domain';
import { RecommendUseCase } from './concierge.use-case';

export type ConciergeStatus = 'idle' | 'loading' | 'ready' | 'error';

@Injectable()
export class ConciergeStore {
  private readonly recommend = inject(RecommendUseCase);

  private readonly _query = signal('beach in Montego Bay in March');
  private readonly _result = signal<ConciergeView | null>(null);
  private readonly _status = signal<ConciergeStatus>('idle');
  private readonly _error = signal<AppError | null>(null);

  readonly query = this._query.asReadonly();
  readonly result = this._result.asReadonly();
  readonly status = this._status.asReadonly();
  readonly error = this._error.asReadonly();

  setQuery(text: string): void {
    this._query.set(text);
  }

  async search(): Promise<void> {
    this._status.set('loading');
    this._error.set(null);

    const result = await this.recommend.execute({ text: this._query().trim() });
    if (!result.ok) {
      this._result.set(null);
      this._error.set(result.error);
      this._status.set('error');
      return;
    }

    this._result.set(result.value);
    this._status.set('ready');
  }
}
