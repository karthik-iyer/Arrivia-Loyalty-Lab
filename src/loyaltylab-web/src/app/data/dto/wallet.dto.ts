import type { LedgerTransactionType } from '../../domain';
import type { MoneyDto } from './money.dto';

export interface WalletBalanceDto {
  readonly memberId: string;
  readonly credits: number;
  readonly monetaryValue: MoneyDto;
  readonly burnCap: number;
}

export interface StatementLineDto {
  readonly id: string;
  readonly type: LedgerTransactionType;
  readonly occurredAt: string;
  readonly reason: string;
  readonly credits: number;
  readonly runningBalance: number;
  readonly reversesTransactionId?: string | null;
}

export interface WalletStatementDto {
  readonly memberId: string;
  readonly balance: number;
  readonly lines: readonly StatementLineDto[];
}
