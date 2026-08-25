import type { Money } from './money';

export type LedgerTransactionType = 'Earn' | 'Burn' | 'Expire' | 'Reversal' | 'Adjustment';

export interface WalletBalanceView {
  readonly memberId: string;
  readonly credits: number;
  readonly monetaryValue: Money;
  readonly burnCap: number;
}

export interface StatementLineView {
  readonly id: string;
  readonly type: LedgerTransactionType;
  readonly occurredAt: string;
  readonly reason: string;
  readonly credits: number;
  readonly runningBalance: number;
  readonly reversesTransactionId: string | null;
}

export interface WalletStatementView {
  readonly memberId: string;
  readonly balance: number;
  readonly lines: readonly StatementLineView[];
}
