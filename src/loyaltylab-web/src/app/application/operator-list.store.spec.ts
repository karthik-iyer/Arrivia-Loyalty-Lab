import { TestBed } from '@angular/core/testing';

import { ok, OPERATOR_PORT } from '../domain';
import { OperatorListStore } from './operator-list.store';
import { ListSagasUseCase, RunAdminWorkerUseCase } from './operator.use-case';
import { confirmedSagaItem, reviewSagaItem } from './test-fixtures';

describe('OperatorListStore', () => {
  it('surfaces RequiresManualReview ahead of later confirmed sagas', async () => {
    TestBed.configureTestingModule({
      providers: [
        OperatorListStore,
        ListSagasUseCase,
        RunAdminWorkerUseCase,
        {
          provide: OPERATOR_PORT,
          useValue: {
            listSagas: async () => ok([confirmedSagaItem, reviewSagaItem]),
          },
        },
      ],
    });

    const store = TestBed.inject(OperatorListStore);
    await store.load();

    expect(store.visible().map((saga) => saga.status)).toEqual([
      'RequiresManualReview',
      'Confirmed',
    ]);
  });

  it('records members scanned after an opportunity scan', async () => {
    TestBed.configureTestingModule({
      providers: [
        OperatorListStore,
        ListSagasUseCase,
        RunAdminWorkerUseCase,
        {
          provide: OPERATOR_PORT,
          useValue: {
            listSagas: async () => ok([]),
            runWorker: async () => ok({ worker: 'scan', processed: 1 }),
          },
        },
      ],
    });

    const store = TestBed.inject(OperatorListStore);
    await store.runScan();

    expect(store.scanResult()?.worker).toBe('scan');
    expect(store.scanResult()?.processed).toBe(1);
  });
});
