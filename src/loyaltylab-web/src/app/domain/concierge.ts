import type { Money } from './money';

export type ExclusionReason =
  | 'SupplierNotPermitted'
  | 'TierNotEntitled'
  | 'OutsideAvailability'
  | 'UnaffordableWithCredits'
  | 'BudgetExceeded'
  | 'DestinationMismatch';

export interface ConciergeRequest {
  readonly text: string;
  readonly stayDate?: string;
}

export interface RecommendationItemView {
  readonly offerId: string;
  readonly propertyName: string;
  readonly quoteId: string;
  readonly memberPrice: Money;
  readonly creditsCover: number;
  readonly score: number;
  readonly reasons: readonly string[];
}

export interface ExclusionView {
  readonly offerId: string;
  readonly reason: ExclusionReason;
  readonly detail: string;
}

export interface RankingWeightsView {
  readonly valueForMoney: number;
  readonly creditCoverage: number;
  readonly tagMatch: number;
  readonly starRating: number;
}

export interface RecommendationAuditView {
  readonly candidatesConsidered: number;
  readonly candidatesReturned: number;
  readonly interpretedTerms: readonly string[];
  readonly exclusions: readonly ExclusionView[];
  readonly weights: RankingWeightsView;
  readonly narrationApplied: boolean;
}

export interface ConciergeView {
  readonly narrative: string;
  readonly narrationApplied: boolean;
  readonly recommendations: readonly RecommendationItemView[];
  readonly audit: RecommendationAuditView;
}
