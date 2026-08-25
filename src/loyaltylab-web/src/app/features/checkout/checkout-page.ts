import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { CheckoutStore } from '../../application/checkout.store';
import { formatMoney } from '../../shared/money';
import { SagaTimeline } from './saga-timeline';

@Component({
  selector: 'll-checkout-page',
  imports: [RouterLink, SagaTimeline],
  templateUrl: './checkout-page.html',
  styleUrl: './checkout-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [CheckoutStore],
})
export class CheckoutPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly store = inject(CheckoutStore);
  money = formatMoney;

  ngOnInit(): void {
    const quoteId = this.route.snapshot.paramMap.get('quoteId');
    if (quoteId) {
      void this.store.load(quoteId);
    }
  }

  onSlider(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLInputElement) {
      this.store.setCredits(Number(target.value));
    }
  }

  onForceDecline(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLInputElement) {
      this.store.setForceDecline(target.checked);
    }
  }

  creditValueText(): string {
    return `${this.store.credits()} credits`;
  }
}
