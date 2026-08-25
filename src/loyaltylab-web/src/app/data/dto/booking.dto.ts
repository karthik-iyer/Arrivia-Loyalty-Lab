import type {
  BookingStatus,
  CompensationStatus,
  RateDriftKind,
  SagaStatus,
  SagaStepKind,
  SagaStepStatus,
} from '../../domain';
import type { MoneyDto } from './money.dto';

export interface TenderDto {
  readonly cash: MoneyDto;
  readonly credits: number;
}

export interface DriftDto {
  readonly applied: RateDriftKind;
  readonly netRateDelta?: MoneyDto | null;
}

export interface CompensationDto {
  readonly status: CompensationStatus;
  readonly attempts: number;
  readonly externalReference?: string | null;
  readonly errorCode?: string | null;
}

export interface SagaStepDto {
  readonly kind: SagaStepKind;
  readonly status: SagaStepStatus;
  readonly attempts: number;
  readonly externalReference?: string | null;
  readonly errorCode?: string | null;
  readonly startedAt?: string | null;
  readonly completedAt?: string | null;
  readonly durationMs?: number | null;
  readonly compensation?: CompensationDto | null;
}

export interface SagaDto {
  readonly id: string;
  readonly status: SagaStatus;
  readonly steps: readonly SagaStepDto[];
}

export interface BookingDto {
  readonly bookingId: string;
  readonly status: BookingStatus;
  readonly tender: TenderDto;
  readonly drift?: DriftDto | null;
  readonly saga: SagaDto;
}

export interface CreateBookingDto {
  readonly quoteId: string;
  readonly credits: number;
  readonly stayDate?: string;
}
