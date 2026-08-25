import type { Money } from '../../domain';
import type { MoneyDto } from '../dto/money.dto';

export function toMoney(dto: MoneyDto): Money {
  return { amount: dto.amount, currency: dto.currency };
}

export function toMoneyOrNull(dto: MoneyDto | null | undefined): Money | null {
  return dto == null ? null : toMoney(dto);
}
