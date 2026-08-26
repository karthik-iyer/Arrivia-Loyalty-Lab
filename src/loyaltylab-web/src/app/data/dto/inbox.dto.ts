import type { MoneyDto } from './money.dto';

export interface NudgeSignalDto {
  readonly kind: string;
  readonly rawValue: number;
  readonly normalized: number;
  readonly weight: number;
  readonly contribution: number;
}

export interface NudgeDto {
  readonly nudgeId: string;
  readonly offerId: string;
  readonly propertyName: string;
  readonly windowStart: string;
  readonly windowEnd: string;
  readonly score: number;
  readonly expiresAt: string;
  readonly signals: readonly NudgeSignalDto[];
}

export interface InboxDto {
  readonly nudges: readonly NudgeDto[];
}

export interface ActionedNudgeDto {
  readonly nudgeId: string;
  readonly quoteId: string;
  readonly offerId: string;
  readonly memberPrice: MoneyDto;
  readonly maxCreditTender: MoneyDto;
  readonly maxCredits: number;
  readonly expiresAt: string;
}
