import { TestBed } from '@angular/core/testing';

import { CONCIERGE_PORT, err, ok } from '../domain';
import { ConciergeStore } from './concierge.store';
import { RecommendUseCase } from './concierge.use-case';
import { coralConcierge } from './test-fixtures';

describe('ConciergeStore', () => {
  it('loads recommendations from the concierge port', async () => {
    TestBed.configureTestingModule({
      providers: [
        ConciergeStore,
        RecommendUseCase,
        {
          provide: CONCIERGE_PORT,
          useValue: {
            recommend: async () => ok(coralConcierge),
          },
        },
      ],
    });

    const store = TestBed.inject(ConciergeStore);
    await store.search();

    expect(store.status()).toBe('ready');
    expect(store.result()?.recommendations[0]?.propertyName).toBe('Coral Bay Resort');
    expect(store.result()?.audit.exclusions[0]?.reason).toBe('UnaffordableWithCredits');
    expect(store.result()?.narrationApplied).toBe(false);
  });

  it('surfaces a port failure without calling HTTP', async () => {
    TestBed.configureTestingModule({
      providers: [
        ConciergeStore,
        RecommendUseCase,
        {
          provide: CONCIERGE_PORT,
          useValue: {
            recommend: async () =>
              err({
                errorCode: 'MEMBER_NOT_FOUND',
                message: 'Sign in to use the concierge.',
                status: 404,
                correlationId: 'c1',
              }),
          },
        },
      ],
    });

    const store = TestBed.inject(ConciergeStore);
    await store.search();

    expect(store.status()).toBe('error');
    expect(store.error()?.errorCode).toBe('MEMBER_NOT_FOUND');
    expect(store.result()).toBeNull();
  });
});
