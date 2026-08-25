import { inject, Injectable, signal } from '@angular/core';

import type { AppError, WalletBalanceView, WalletStatementView } from '../domain';
import { GetBalanceUseCase } from './booking.use-case';
import { GetStatementUseCase } from './wallet.use-case';

export type WalletStatus = 'idle' | 'loading' | 'ready' | 'error';

@Injectable()
export class WalletStore {
  private readonly getBalance = inject(GetBalanceUseCase);
  private readonly getStatement = inject(GetStatementUseCase);

  private readonly _balance = signal<WalletBalanceView | null>(null);
  private readonly _statement = signal<WalletStatementView | null>(null);
  private readonly _status = signal<WalletStatus>('idle');
  private readonly _error = signal<AppError | null>(null);

  readonly balance = this._balance.asReadonly();
  readonly statement = this._statement.asReadonly();
  readonly status = this._status.asReadonly();
  readonly error = this._error.asReadonly();

  async load(): Promise<void> {
    this._status.set('loading');
    this._error.set(null);

    const [balance, statement] = await Promise.all([
      this.getBalance.execute(),
      this.getStatement.execute(),
    ]);

    if (!balance.ok) {
      this._error.set(balance.error);
      this._status.set('error');
      return;
    }

    if (!statement.ok) {
      this._error.set(statement.error);
      this._status.set('error');
      return;
    }

    this._balance.set(balance.value);
    this._statement.set(statement.value);
    this._status.set('ready');
  }
}
