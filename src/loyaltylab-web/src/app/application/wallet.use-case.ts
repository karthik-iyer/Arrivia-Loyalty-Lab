import { inject, Injectable } from '@angular/core';

import { WALLET_PORT, type Result, type WalletStatementView } from '../domain';

@Injectable({ providedIn: 'root' })
export class GetStatementUseCase {
  private readonly wallet = inject(WALLET_PORT);

  execute(): Promise<Result<WalletStatementView>> {
    return this.wallet.statement();
  }
}
