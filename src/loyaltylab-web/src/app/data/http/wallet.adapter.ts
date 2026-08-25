import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { Result, WalletBalanceView, WalletPort, WalletStatementView } from '../../domain';
import type { WalletBalanceDto, WalletStatementDto } from '../dto/wallet.dto';
import { toWalletBalanceView, toWalletStatementView } from '../mappers/wallet.mapper';
import { HttpResult } from './http-result';

@Injectable()
export class HttpWalletAdapter implements WalletPort {
  private readonly http = inject(HttpClient);
  private readonly results = inject(HttpResult);

  balance(): Promise<Result<WalletBalanceView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.get<WalletBalanceDto>('/api/wallet/balance'));
      return toWalletBalanceView(dto);
    });
  }

  statement(): Promise<Result<WalletStatementView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.get<WalletStatementDto>('/api/wallet/statement'));
      return toWalletStatementView(dto);
    });
  }
}
