import type { Money } from '../domain';

export function formatMoney(money: Money): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: money.currency,
  }).format(money.amount);
}

export function formatMoneyDelta(before: Money, after: Money): string {
  const delta = after.amount - before.amount;
  const formatted = formatMoney({ amount: Math.abs(delta), currency: after.currency });
  if (delta > 0) {
    return `+${formatted}`;
  }

  if (delta < 0) {
    return `−${formatted}`;
  }

  return formatted;
}
