import { TestBed } from '@angular/core/testing';

import { CATALOG_PORT, ok } from '../domain';
import { CatalogStore } from './catalog.store';
import { SearchOffersUseCase } from './search-offers.use-case';
import { anonymousCoral, coralOffer } from './test-fixtures';

describe('CatalogStore', () => {
  it('filters by tag without touching HTTP', async () => {
    TestBed.configureTestingModule({
      providers: [
        CatalogStore,
        SearchOffersUseCase,
        {
          provide: CATALOG_PORT,
          useValue: { search: async () => ok([coralOffer, { ...coralOffer, offerId: '2', tags: ['Ski'], propertyName: 'Matterhorn Lodge', destination: 'Zermatt' }]) },
        },
      ],
    });

    const store = TestBed.inject(CatalogStore);
    await store.load();
    store.setTag('Beach');

    expect(store.visible().map((offer) => offer.propertyName)).toEqual(['Coral Bay Resort']);
  });

  it('keeps a null memberPrice for anonymous catalog rows', async () => {
    TestBed.configureTestingModule({
      providers: [
        CatalogStore,
        SearchOffersUseCase,
        { provide: CATALOG_PORT, useValue: { search: async () => ok([anonymousCoral]) } },
      ],
    });

    const store = TestBed.inject(CatalogStore);
    await store.load();

    expect(store.visible()[0]?.memberPrice).toBeNull();
  });
});
