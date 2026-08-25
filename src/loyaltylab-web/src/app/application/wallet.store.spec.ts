import { TestBed } from '@angular/core/testing';

import { err, ok, WALLET_PORT } from '../domain';
import { GetBalanceUseCase } from './booking.use-case';
import { mayaBalance, mayaStatement } from './test-fixtures';
import { GetStatementUseCase } from './wallet.use-case';
import { WalletStore } from './wallet.store';

describe('WalletStore', () => {
  it('loads balance and statement from the wallet port', async () => {
    TestBed.configureTestingModule({
      providers: [
        WalletStore,
        GetBalanceUseCase,
        GetStatementUseCase,
        {
          provide: WALLET_PORT,
          useValue: {
            balance: async () => ok(mayaBalance),
            statement: async () => ok(mayaStatement),
          },
        },
      ],
    });

    const store = TestBed.inject(WalletStore);
    await store.load();

    expect(store.status()).toBe('ready');
    expect(store.balance()?.credits).toBe(6000);
    expect(store.statement()?.lines).toHaveLength(3);
    expect(store.statement()?.lines[2]?.reversesTransactionId).toBe(
      store.statement()?.lines[1]?.id,
    );
  });

  it('surfaces a port failure without calling HTTP', async () => {
    TestBed.configureTestingModule({
      providers: [
        WalletStore,
        GetBalanceUseCase,
        GetStatementUseCase,
        {
          provide: WALLET_PORT,
          useValue: {
            balance: async () => ok(mayaBalance),
            statement: async () =>
              err({
                errorCode: 'MEMBER_NOT_FOUND',
                message: 'Sign in to see your wallet.',
                status: 404,
                correlationId: 'c1',
              }),
          },
        },
      ],
    });

    const store = TestBed.inject(WalletStore);
    await store.load();

    expect(store.status()).toBe('error');
    expect(store.error()?.errorCode).toBe('MEMBER_NOT_FOUND');
    expect(store.statement()).toBeNull();
  });
});
