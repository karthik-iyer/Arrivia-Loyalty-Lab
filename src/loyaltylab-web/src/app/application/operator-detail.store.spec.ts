import { TestBed } from '@angular/core/testing';

import { ok, OPERATOR_PORT } from '../domain';
import { OperatorDetailStore } from './operator-detail.store';
import { GetSagaUseCase } from './operator.use-case';
import { reviewNeededSaga } from './test-fixtures';

describe('OperatorDetailStore', () => {
  it('flags RequiresManualReview so the failing step can be highlighted', async () => {
    TestBed.configureTestingModule({
      providers: [
        OperatorDetailStore,
        GetSagaUseCase,
        { provide: OPERATOR_PORT, useValue: { getSaga: async () => ok(reviewNeededSaga) } },
      ],
    });

    const store = TestBed.inject(OperatorDetailStore);
    await store.load(reviewNeededSaga.id);

    expect(store.status()).toBe('ready');
    expect(store.needsReview()).toBe(true);
    expect(store.detail()?.poison[0]?.lastError).toBe('TIMEOUT');
  });
});
