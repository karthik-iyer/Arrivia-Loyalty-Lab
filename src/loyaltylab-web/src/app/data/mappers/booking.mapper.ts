import type {
  BookingView,
  CompensationView,
  CreateBookingRequest,
  DriftView,
  SagaStepView,
  SagaView,
  TenderView,
} from '../../domain';
import type {
  BookingDto,
  CompensationDto,
  CreateBookingDto,
  DriftDto,
  SagaDto,
  SagaStepDto,
  TenderDto,
} from '../dto/booking.dto';
import { toMoney, toMoneyOrNull } from './money.mapper';

export function toCreateBookingDto(request: CreateBookingRequest): CreateBookingDto {
  return {
    quoteId: request.quoteId,
    credits: request.credits,
    ...(request.stayDate === undefined ? {} : { stayDate: request.stayDate }),
  };
}

export function toBookingView(dto: BookingDto): BookingView {
  return {
    bookingId: dto.bookingId,
    status: dto.status,
    tender: toTenderView(dto.tender),
    drift: dto.drift == null ? null : toDriftView(dto.drift),
    saga: toSagaView(dto.saga),
  };
}

export function toSagaView(dto: SagaDto): SagaView {
  return {
    id: dto.id,
    status: dto.status,
    steps: dto.steps.map(toSagaStepView),
  };
}

export function toSagaStepView(dto: SagaStepDto): SagaStepView {
  return {
    kind: dto.kind,
    status: dto.status,
    attempts: dto.attempts,
    externalReference: dto.externalReference ?? null,
    error: dto.errorCode ?? null,
    durationMs: dto.durationMs ?? null,
    compensation: dto.compensation == null ? null : toCompensationView(dto.compensation),
  };
}

function toTenderView(dto: TenderDto): TenderView {
  return { cash: toMoney(dto.cash), credits: dto.credits };
}

function toDriftView(dto: DriftDto): DriftView {
  return { applied: dto.applied, netRateDelta: toMoneyOrNull(dto.netRateDelta) };
}

function toCompensationView(dto: CompensationDto): CompensationView {
  return {
    status: dto.status,
    attempts: dto.attempts,
    externalReference: dto.externalReference ?? null,
    errorCode: dto.errorCode ?? null,
  };
}
