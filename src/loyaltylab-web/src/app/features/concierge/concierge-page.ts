import { ChangeDetectionStrategy, Component, ElementRef, inject, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ConciergeStore } from '../../application/concierge.store';
import type { RecommendationItemView } from '../../domain';
import { formatMoney } from '../../shared/money';
import { AuditPanel } from './audit-panel';

@Component({
  selector: 'll-concierge-page',
  imports: [RouterLink, AuditPanel],
  templateUrl: './concierge-page.html',
  styleUrl: './concierge-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ConciergeStore],
})
export class ConciergePage {
  readonly store = inject(ConciergeStore);
  private readonly results = viewChild<ElementRef<HTMLElement>>('results');

  money = formatMoney;

  onInput(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLTextAreaElement) {
      this.store.setQuery(target.value);
    }
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    await this.store.search();
    this.results()?.nativeElement.focus();
  }

  scoreLabel(item: RecommendationItemView): string {
    return `${Math.round(item.score * 100)}% match`;
  }
}
