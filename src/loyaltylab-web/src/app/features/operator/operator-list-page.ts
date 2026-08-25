import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';

import { OperatorListStore, type SagaStatusFilter } from '../../application/operator-list.store';
import type { SagaStatus } from '../../domain';

const FILTERS: readonly SagaStatusFilter[] = [
  'all',
  'RequiresManualReview',
  'Running',
  'Compensating',
  'Compensated',
  'Confirmed',
];

@Component({
  selector: 'll-operator-list-page',
  imports: [RouterLink],
  templateUrl: './operator-list-page.html',
  styleUrl: './operator-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [OperatorListStore],
})
export class OperatorListPage implements OnInit {
  readonly store = inject(OperatorListStore);
  readonly filters = FILTERS;

  ngOnInit(): void {
    void this.store.load();
  }

  onFilter(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      this.store.setFilter(target.value as SagaStatusFilter);
    }
  }

  filterLabel(filter: SagaStatusFilter): string {
    return filter === 'all' ? 'All statuses' : filter;
  }

  rowClass(status: SagaStatus): string {
    return status === 'RequiresManualReview' ? 'row row--review' : 'row';
  }
}
