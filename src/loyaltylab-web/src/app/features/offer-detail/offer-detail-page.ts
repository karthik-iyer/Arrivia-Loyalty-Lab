import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { OfferDetailStore } from '../../application/offer-detail.store';
import { formatMoney } from '../../shared/money';
import { ExplanationPanel } from './explanation-panel';

@Component({
  selector: 'll-offer-detail-page',
  imports: [RouterLink, ExplanationPanel],
  templateUrl: './offer-detail-page.html',
  styleUrl: './offer-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [OfferDetailStore],
})
export class OfferDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly store = inject(OfferDetailStore);

  money = formatMoney;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      void this.store.load(id);
    }
  }
}
