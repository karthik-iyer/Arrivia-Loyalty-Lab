import type { Money } from './money';

export type BookingStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Failed';

export type SagaStatus =
  | 'Running'
  | 'Compensating'
  | 'Confirmed'
  | 'Compensated'
  | 'RequiresManualReview';

export type SagaStepKind =
  | 'ValidateQuote'
  | 'ReserveInventory'
  | 'AuthorizePayment'
  | 'BurnCredits'
  | 'CapturePayment'
  | 'ConfirmBooking';

export type SagaStepStatus =
  | 'Pending'
  | 'InProgress'
  | 'Succeeded'
  | 'Failed'
  | 'Unknown'
  | 'Compensated'
  | 'CompensationFailed';

export type CompensationStatus = 'Pending' | 'Succeeded' | 'Failed';

export type RateDriftKind = 'Unchanged' | 'Absorbed';

export interface TenderView {
  readonly cash: Money;
  readonly credits: number;
}

export interface DriftView {
  readonly applied: RateDriftKind;
  readonly netRateDelta: Money | null;
}

export interface CompensationView {
  readonly status: CompensationStatus;
  readonly attempts: number;
  readonly externalReference: string | null;
  readonly errorCode: string | null;
}

export interface SagaStepView {
  readonly kind: SagaStepKind;
  readonly status: SagaStepStatus;
  readonly attempts: number;
  readonly externalReference: string | null;
  readonly error: string | null;
  readonly durationMs: number | null;
  readonly compensation: CompensationView | null;
}

export interface SagaView {
  readonly id: string;
  readonly status: SagaStatus;
  readonly steps: readonly SagaStepView[];
}

export interface BookingView {
  readonly bookingId: string;
  readonly status: BookingStatus;
  readonly tender: TenderView;
  readonly drift: DriftView | null;
  readonly saga: SagaView;
}

export interface CreateBookingRequest {
  readonly quoteId: string;
  readonly credits: number;
  readonly stayDate?: string;
}
