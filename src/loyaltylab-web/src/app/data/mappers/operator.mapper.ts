import type { AdminWorkerView, PoisonMessageView, SagaListItemView, SagaOperatorView } from '../../domain';
import type { AdminWorkerDto, PoisonDto, SagaListItemDto, SagaOperatorDto } from '../dto/operator.dto';
import { toSagaStepView } from './booking.mapper';

export function toSagaListItemView(dto: SagaListItemDto): SagaListItemView {
  return {
    id: dto.id,
    bookingId: dto.bookingId,
    status: dto.status,
    startedAt: dto.startedAt,
    lastHeartbeatAt: dto.lastHeartbeatAt,
  };
}

export function toSagaOperatorView(dto: SagaOperatorDto): SagaOperatorView {
  return {
    id: dto.id,
    bookingId: dto.bookingId,
    status: dto.status,
    startedAt: dto.startedAt,
    lastHeartbeatAt: dto.lastHeartbeatAt,
    completedAt: dto.completedAt ?? null,
    steps: dto.steps.map(toSagaStepView),
    poison: dto.poison.map(toPoisonMessageView),
  };
}

export function toAdminWorkerView(dto: AdminWorkerDto): AdminWorkerView {
  return { worker: dto.worker, processed: dto.processed };
}

function toPoisonMessageView(dto: PoisonDto): PoisonMessageView {
  return {
    id: dto.id,
    type: dto.type,
    correlationId: dto.correlationId,
    attempts: dto.attempts,
    lastError: dto.lastError,
    poisonedAt: dto.poisonedAt,
  };
}
