import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CatalogStore, type TagFilter } from '../../application/catalog.store';
import type { OfferSummary } from '../../domain';
import { formatMoney } from '../../shared/money';

const TAG_FILTERS: readonly TagFilter[] = ['all', 'Beach', 'Ski', 'City', 'Family', 'Luxury'];

@Component({
  selector: 'll-catalog-page',
  imports: [RouterLink],
  templateUrl: './catalog-page.html',
  styleUrl: './catalog-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [CatalogStore],
})
export class CatalogPage implements OnInit {
  readonly store = inject(CatalogStore);
  readonly tags = TAG_FILTERS;

  ngOnInit(): void {
    void this.store.load();
  }

  priceLabel(offer: OfferSummary): string {
    return offer.memberPrice === null ? 'Sign in to see member price' : formatMoney(offer.memberPrice);
  }

  onTag(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      this.store.setTag(target.value as TagFilter);
    }
  }

  onDestination(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      this.store.setDestination(target.value);
    }
  }
}
