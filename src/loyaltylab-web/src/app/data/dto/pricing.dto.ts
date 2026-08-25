import type { PricingStageKind } from '../../domain';
import type { MoneyDto } from './money.dto';

export interface QuoteDto {
  readonly quoteId: string;
  readonly offerId: string;
  readonly memberPrice: MoneyDto;
  readonly maxCreditTender: MoneyDto;
  readonly maxCredits: number;
  readonly expiresAt: string;
}

export interface TraceDto {
  readonly stage: PricingStageKind;
  readonly order: number;
  readonly description: string;
  readonly appliedRule?: string | null;
  readonly subtotalBefore: MoneyDto;
  readonly subtotalAfter: MoneyDto;
  readonly wasClamped: boolean;
  readonly clampReason?: string | null;
}

export interface ExplainDto {
  readonly stages: readonly TraceDto[];
  readonly memberPrice: MoneyDto;
  readonly maxCreditTender?: MoneyDto | null;
  readonly netCost?: MoneyDto | null;
  readonly margin?: MoneyDto | null;
}
