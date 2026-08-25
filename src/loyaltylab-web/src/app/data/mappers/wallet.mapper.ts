import type { StatementLineView, WalletBalanceView, WalletStatementView } from '../../domain';
import type { StatementLineDto, WalletBalanceDto, WalletStatementDto } from '../dto/wallet.dto';
import { toMoney } from './money.mapper';

export function toWalletBalanceView(dto: WalletBalanceDto): WalletBalanceView {
  return {
    memberId: dto.memberId,
    credits: dto.credits,
    monetaryValue: toMoney(dto.monetaryValue),
    burnCap: dto.burnCap,
  };
}

export function toWalletStatementView(dto: WalletStatementDto): WalletStatementView {
  return {
    memberId: dto.memberId,
    balance: dto.balance,
    lines: dto.lines.map(toStatementLineView),
  };
}

function toStatementLineView(dto: StatementLineDto): StatementLineView {
  return {
    id: dto.id,
    type: dto.type,
    occurredAt: dto.occurredAt,
    reason: dto.reason,
    credits: dto.credits,
    runningBalance: dto.runningBalance,
    reversesTransactionId: dto.reversesTransactionId ?? null,
  };
}
