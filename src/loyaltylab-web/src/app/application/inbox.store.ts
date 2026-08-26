import { inject, Injectable, signal } from '@angular/core';

import type { AppError, NudgeView } from '../domain';
import { ActionNudgeUseCase, DismissNudgeUseCase, ListInboxUseCase } from './inbox.use-case';

export type InboxStatus = 'idle' | 'loading' | 'ready' | 'error';

@Injectable()
export class InboxStore {
  private readonly listInbox = inject(ListInboxUseCase);
  private readonly actionNudge = inject(ActionNudgeUseCase);
  private readonly dismissNudge = inject(DismissNudgeUseCase);

  private readonly _nudges = signal<readonly NudgeView[]>([]);
  private readonly _status = signal<InboxStatus>('idle');
  private readonly _error = signal<AppError | null>(null);
  private readonly _busyId = signal<string | null>(null);
  private readonly _expiredIds = signal<ReadonlySet<string>>(new Set());

  readonly nudges = this._nudges.asReadonly();
  readonly status = this._status.asReadonly();
  readonly error = this._error.asReadonly();
  readonly busyId = this._busyId.asReadonly();
  readonly expiredIds = this._expiredIds.asReadonly();

  async load(): Promise<void> {
    this._status.set('loading');
    this._error.set(null);
    this._expiredIds.set(new Set());

    const result = await this.listInbox.execute();
    if (!result.ok) {
      this._nudges.set([]);
      this._error.set(result.error);
      this._status.set('error');
      return;
    }

    this._nudges.set(result.value);
    this._status.set('ready');
  }

  async action(nudgeId: string): Promise<string | null> {
    this._busyId.set(nudgeId);
    this._error.set(null);

    const result = await this.actionNudge.execute(nudgeId);
    this._busyId.set(null);

    if (!result.ok) {
      if (result.error.errorCode === 'NUDGE_EXPIRED') {
        this.markExpired(nudgeId);
        return null;
      }

      this._error.set(result.error);
      return null;
    }

    this._nudges.update((rows) => rows.filter((row) => row.nudgeId !== nudgeId));
    return result.value.quoteId;
  }

  async dismiss(nudgeId: string): Promise<void> {
    this._busyId.set(nudgeId);
    this._error.set(null);

    const result = await this.dismissNudge.execute(nudgeId);
    this._busyId.set(null);

    if (!result.ok) {
      if (result.error.errorCode === 'NUDGE_EXPIRED') {
        this.markExpired(nudgeId);
        return;
      }

      this._error.set(result.error);
      return;
    }

    this._nudges.update((rows) => rows.filter((row) => row.nudgeId !== nudgeId));
  }

  isExpired(nudgeId: string): boolean {
    return this._expiredIds().has(nudgeId);
  }

  private markExpired(nudgeId: string): void {
    this._expiredIds.update((ids) => new Set([...ids, nudgeId]));
  }
}
