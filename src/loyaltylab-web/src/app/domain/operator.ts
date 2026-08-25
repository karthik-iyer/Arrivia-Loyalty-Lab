import type { SagaStatus, SagaStepView } from './booking';

export type AdminWorkerName = 'outbox' | 'recovery' | 'expiry';

export interface SagaListItemView {
  readonly id: string;
  readonly bookingId: string;
  readonly status: SagaStatus;
  readonly startedAt: string;
  readonly lastHeartbeatAt: string;
}

export interface PoisonMessageView {
  readonly id: string;
  readonly type: string;
  readonly correlationId: string;
  readonly attempts: number;
  readonly lastError: string;
  readonly poisonedAt: string;
}

export interface SagaOperatorView {
  readonly id: string;
  readonly bookingId: string;
  readonly status: SagaStatus;
  readonly startedAt: string;
  readonly lastHeartbeatAt: string;
  readonly completedAt: string | null;
  readonly steps: readonly SagaStepView[];
  readonly poison: readonly PoisonMessageView[];
}

export interface AdminWorkerView {
  readonly worker: string;
  readonly processed: number;
}
