import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import type { PriceExplanationView, PriceTraceEntry } from '../../domain';
import { formatMoney, formatMoneyDelta } from '../../shared/money';

@Component({
  selector: 'll-explanation-panel',
  templateUrl: './explanation-panel.html',
  styleUrl: './explanation-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExplanationPanel {
  readonly explanation = input.required<PriceExplanationView>();

  money = formatMoney;
  delta = formatMoneyDelta;

  stageLabel(entry: PriceTraceEntry): string {
    return entry.description;
  }
}
