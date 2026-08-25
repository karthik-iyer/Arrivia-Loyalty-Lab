import type { Money } from './money';

export type PricingStageKind =
  | 'Eligibility'
  | 'BaseCost'
  | 'BaseMarkup'
  | 'TierAdjustment'
  | 'CampaignDiscount'
  | 'MarginFloor'
  | 'Rounding'
  | 'BurnCap';

export interface PriceTraceEntry {
  readonly stage: PricingStageKind;
  readonly order: number;
  readonly description: string;
  readonly appliedRule: string | null;
  readonly subtotalBefore: Money;
  readonly subtotalAfter: Money;
  readonly wasClamped: boolean;
  readonly clampReason: string | null;
}

export interface QuoteView {
  readonly quoteId: string;
  readonly offerId: string;
  readonly memberPrice: Money;
  readonly maxCreditTender: Money;
  readonly maxCredits: number;
  readonly expiresAt: string;
}

export interface PriceExplanationView {
  readonly stages: readonly PriceTraceEntry[];
  readonly memberPrice: Money;
  readonly maxCreditTender: Money | null;
  /** Absent for member and anonymous roles (NFR-04). */
  readonly netCost: Money | null;
  readonly margin: Money | null;
}

export interface QuoteOfferRequest {
  readonly stayDate?: string;
}
