import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import type { SagaStepKind, SagaStepView } from '../../domain';

const labels: Record<SagaStepKind, string> = {
  ValidateQuote: 'Validate quote',
  ReserveInventory: 'Reserve inventory',
  AuthorizePayment: 'Authorize payment',
  BurnCredits: 'Burn credits',
  CapturePayment: 'Capture payment',
  ConfirmBooking: 'Confirm booking',
};

@Component({
  selector: 'll-saga-timeline',
  templateUrl: './saga-timeline.html',
  styleUrl: './saga-timeline.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SagaTimeline {
  readonly steps = input.required<readonly SagaStepView[]>();
  readonly live = input(false);
  readonly verbose = input(false);
  readonly highlightFailures = input(false);

  label(kind: SagaStepKind): string {
    return labels[kind];
  }

  hasDuration(step: SagaStepView): boolean {
    return step.durationMs !== null;
  }

  mark(step: SagaStepView): string {
    switch (step.status) {
      case 'Succeeded':
      case 'Compensated':
        return '✓';
      case 'InProgress':
        return '⟳';
      case 'Failed':
      case 'CompensationFailed':
        return '✗';
      case 'Unknown':
        return '?';
      default:
        return '·';
    }
  }

  isFailing(step: SagaStepView): boolean {
    return step.status === 'Failed' || step.status === 'CompensationFailed';
  }

  showAttempts(step: SagaStepView): boolean {
    return this.verbose() || step.attempts > 1;
  }
}
