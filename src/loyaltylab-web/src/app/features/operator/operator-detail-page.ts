import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { OperatorDetailStore } from '../../application/operator-detail.store';
import { SagaTimeline } from '../checkout/saga-timeline';

@Component({
  selector: 'll-operator-detail-page',
  imports: [RouterLink, SagaTimeline],
  templateUrl: './operator-detail-page.html',
  styleUrl: './operator-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [OperatorDetailStore],
})
export class OperatorDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly store = inject(OperatorDetailStore);

  ngOnInit(): void {
    const sagaId = this.route.snapshot.paramMap.get('id');
    if (sagaId) {
      void this.store.load(sagaId);
    }
  }
}
