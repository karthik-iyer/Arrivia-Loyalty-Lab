import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';

import { WalletStore } from '../../application/wallet.store';
import type { StatementLineView } from '../../domain';
import { formatMoney } from '../../shared/money';

@Component({
  selector: 'll-wallet-page',
  imports: [RouterLink],
  templateUrl: './wallet-page.html',
  styleUrl: './wallet-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [WalletStore],
})
export class WalletPage implements OnInit {
  readonly store = inject(WalletStore);
  money = formatMoney;

  ngOnInit(): void {
    void this.store.load();
  }

  rowId(line: StatementLineView): string {
    return `txn-${line.id}`;
  }

  originalHref(line: StatementLineView): string | null {
    return line.reversesTransactionId ? `#txn-${line.reversesTransactionId}` : null;
  }

  signedCredits(line: StatementLineView): string {
    const sign = line.credits > 0 ? '+' : '';
    return `${sign}${line.credits}`;
  }

  occurredOn(iso: string): string {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) {
      return iso;
    }

    return date.toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' });
  }
}
