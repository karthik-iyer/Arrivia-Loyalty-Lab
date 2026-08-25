import { toWalletBalanceView, toWalletStatementView } from './wallet.mapper';
import type { WalletBalanceDto, WalletStatementDto } from '../dto/wallet.dto';

/** Captured GET /api/wallet/balance for Maya. */
const balanceJson = `{
  "memberId": "a11ce001-0002-7000-8000-000000000001",
  "credits": 6000,
  "monetaryValue": { "amount": 60, "currency": "USD" },
  "burnCap": 40
}`;

/** Captured GET /api/wallet/statement including a reversal link (FR-L-12). */
const statementJson = `{
  "memberId": "a11ce001-0002-7000-8000-000000000001",
  "balance": 6000,
  "lines": [
    {
      "id": "a11ce001-0009-7000-8000-000000000001",
      "type": "Earn",
      "occurredAt": "2026-03-01T00:00:00+00:00",
      "reason": "Opening grant",
      "credits": 6000,
      "runningBalance": 6000
    },
    {
      "id": "a11ce001-0009-7000-8000-000000000002",
      "type": "Burn",
      "occurredAt": "2026-03-15T12:00:00+00:00",
      "reason": "Booking tender",
      "credits": -4830,
      "runningBalance": 1170
    },
    {
      "id": "a11ce001-0009-7000-8000-000000000003",
      "type": "Reversal",
      "occurredAt": "2026-03-15T12:04:00+00:00",
      "reason": "Capture failed",
      "credits": 4830,
      "runningBalance": 6000,
      "reversesTransactionId": "a11ce001-0009-7000-8000-000000000002"
    }
  ]
}`;

describe('wallet mapper', () => {
  it('maps Maya seeded opening grant', () => {
    const view = toWalletBalanceView(JSON.parse(balanceJson) as WalletBalanceDto);

    expect(view.credits).toBe(6000);
    expect(view.monetaryValue).toEqual({ amount: 60, currency: 'USD' });
    expect(view.burnCap).toBe(40);
  });

  it('maps a reversal onto the transaction it reverses', () => {
    const view = toWalletStatementView(JSON.parse(statementJson) as WalletStatementDto);
    const original = view.lines[1];
    const reversal = view.lines[2];

    expect(original?.id).toBe('a11ce001-0009-7000-8000-000000000002');
    expect(reversal?.type).toBe('Reversal');
    expect(reversal?.reversesTransactionId).toBe(original?.id);
  });
});
