import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import type { RecommendationAuditView } from '../../domain';

@Component({
  selector: 'll-audit-panel',
  templateUrl: './audit-panel.html',
  styleUrl: './audit-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditPanel {
  readonly audit = input.required<RecommendationAuditView>();

  percent(weight: number): string {
    return `${Math.round(weight * 100)}%`;
  }

  reasonLabel(reason: string): string {
    return reason.replace(/([A-Z])/g, ' $1').trim();
  }
}
