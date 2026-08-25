import type { SagaStatus } from '../../domain';
import type { SagaStepDto } from './booking.dto';

export interface SagaListItemDto {
  readonly id: string;
  readonly bookingId: string;
  readonly status: SagaStatus;
  readonly startedAt: string;
  readonly lastHeartbeatAt: string;
}

export interface PoisonDto {
  readonly id: string;
  readonly type: string;
  readonly correlationId: string;
  readonly attempts: number;
  readonly lastError: string;
  readonly poisonedAt: string;
}

export interface SagaOperatorDto {
  readonly id: string;
  readonly bookingId: string;
  readonly status: SagaStatus;
  readonly startedAt: string;
  readonly lastHeartbeatAt: string;
  readonly completedAt?: string | null;
  readonly steps: readonly SagaStepDto[];
  readonly poison: readonly PoisonDto[];
}

export interface AdminWorkerDto {
  readonly worker: string;
  readonly processed: number;
}
