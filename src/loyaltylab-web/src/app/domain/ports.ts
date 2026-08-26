import { InjectionToken } from '@angular/core';

import type { BookingView, CreateBookingOptions, CreateBookingRequest } from './booking';
import type { OfferSummary } from './catalog';
import type { ConciergeRequest, ConciergeView } from './concierge';
import type { NudgeView } from './inbox';
import type {
  AdminWorkerName,
  AdminWorkerView,
  SagaListItemView,
  SagaOperatorView,
} from './operator';
import type { PriceExplanationView, QuoteOfferRequest, QuoteView } from './pricing';
import type { Result } from './result';
import type { PartnerThemeView } from './session';
import type { WalletBalanceView, WalletStatementView } from './wallet';

export interface CatalogPort {
  search(stayDate?: string): Promise<Result<readonly OfferSummary[]>>;
}

export interface PricingPort {
  quote(offerId: string, request?: QuoteOfferRequest): Promise<Result<QuoteView>>;
  explain(quoteId: string): Promise<Result<PriceExplanationView>>;
}

export interface BookingPort {
  create(
    request: CreateBookingRequest,
    idempotencyKey: string,
    options?: CreateBookingOptions,
  ): Promise<Result<BookingView>>;
  get(bookingId: string): Promise<Result<BookingView>>;
  cancel(bookingId: string, idempotencyKey: string): Promise<Result<BookingView>>;
}

export interface WalletPort {
  balance(): Promise<Result<WalletBalanceView>>;
  statement(): Promise<Result<WalletStatementView>>;
}

export interface ConciergePort {
  recommend(request: ConciergeRequest): Promise<Result<ConciergeView>>;
}

export interface InboxPort {
  list(): Promise<Result<readonly NudgeView[]>>;
  action(nudgeId: string): Promise<Result<QuoteView>>;
  dismiss(nudgeId: string): Promise<Result<void>>;
}

export interface OperatorPort {
  listSagas(): Promise<Result<readonly SagaListItemView[]>>;
  getSaga(sagaId: string): Promise<Result<SagaOperatorView>>;
  runWorker(worker: AdminWorkerName): Promise<Result<AdminWorkerView>>;
}

export interface PartnerPort {
  theme(): Promise<Result<PartnerThemeView>>;
}

export const PARTNER_PORT = new InjectionToken<PartnerPort>('PartnerPort');
export const CATALOG_PORT = new InjectionToken<CatalogPort>('CatalogPort');
export const PRICING_PORT = new InjectionToken<PricingPort>('PricingPort');
export const BOOKING_PORT = new InjectionToken<BookingPort>('BookingPort');
export const WALLET_PORT = new InjectionToken<WalletPort>('WalletPort');
export const CONCIERGE_PORT = new InjectionToken<ConciergePort>('ConciergePort');
export const INBOX_PORT = new InjectionToken<InboxPort>('InboxPort');
export const OPERATOR_PORT = new InjectionToken<OperatorPort>('OperatorPort');
