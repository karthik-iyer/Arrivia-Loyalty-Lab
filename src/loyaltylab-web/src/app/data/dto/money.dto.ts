export interface MoneyDto {
  readonly amount: number;
  readonly currency: string;
}

export function isMoneyDto(value: unknown): value is MoneyDto {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const record = value as Record<string, unknown>;
  return typeof record['amount'] === 'number' && typeof record['currency'] === 'string';
}
