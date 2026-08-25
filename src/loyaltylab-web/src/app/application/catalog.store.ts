import { computed, inject, Injectable, signal } from '@angular/core';

import type { AppError, OfferSummary, OfferTag } from '../domain';
import { DEMO_STAY_DATE } from './demo-stay';
import { SearchOffersUseCase } from './search-offers.use-case';

export type CatalogStatus = 'idle' | 'loading' | 'ready' | 'error';
export type TagFilter = OfferTag | 'all';

@Injectable()
export class CatalogStore {
  private readonly searchOffers = inject(SearchOffersUseCase);

  private readonly _offers = signal<readonly OfferSummary[]>([]);
  private readonly _status = signal<CatalogStatus>('idle');
  private readonly _error = signal<AppError | null>(null);
  private readonly _tag = signal<TagFilter>('all');
  private readonly _destination = signal<string>('all');
  private readonly _stayDate = signal(DEMO_STAY_DATE);

  readonly offers = this._offers.asReadonly();
  readonly status = this._status.asReadonly();
  readonly error = this._error.asReadonly();
  readonly tag = this._tag.asReadonly();
  readonly destination = this._destination.asReadonly();
  readonly stayDate = this._stayDate.asReadonly();

  readonly destinations = computed(() => {
    const names = new Set(this._offers().map((offer) => offer.destination));
    return [...names].sort((a, b) => a.localeCompare(b));
  });

  readonly visible = computed(() => {
    const tag = this._tag();
    const destination = this._destination();
    return this._offers().filter((offer) => {
      const tagOk = tag === 'all' || offer.tags.includes(tag);
      const destOk = destination === 'all' || offer.destination === destination;
      return tagOk && destOk;
    });
  });

  setTag(tag: TagFilter): void {
    this._tag.set(tag);
  }

  setDestination(destination: string): void {
    this._destination.set(destination);
  }

  async load(stayDate = this._stayDate()): Promise<void> {
    this._stayDate.set(stayDate);
    this._status.set('loading');
    this._error.set(null);
    const result = await this.searchOffers.execute(stayDate);
    if (!result.ok) {
      this._error.set(result.error);
      this._status.set('error');
      return;
    }

    this._offers.set(result.value);
    this._status.set('ready');
  }
}
