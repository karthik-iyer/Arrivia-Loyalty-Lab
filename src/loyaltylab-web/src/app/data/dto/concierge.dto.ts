import type { ExclusionReason } from '../../domain';
import type { MoneyDto } from './money.dto';

export interface RecommendationItemDto {
  readonly offerId: string;
  readonly propertyName: string;
  readonly quoteId: string;
  readonly memberPrice: MoneyDto;
  readonly creditsCover: number;
  readonly score: number;
  readonly reasons: readonly string[];
}

export interface ExclusionDto {
  readonly offerId: string;
  readonly reason: ExclusionReason;
  readonly detail: string;
}

export interface RankingWeightsDto {
  readonly valueForMoney: number;
  readonly creditCoverage: number;
  readonly tagMatch: number;
  readonly starRating: number;
}

export interface RecommendationAuditDto {
  readonly candidatesConsidered: number;
  readonly candidatesReturned: number;
  readonly interpretedTerms: readonly string[];
  readonly exclusions: readonly ExclusionDto[];
  readonly weights: RankingWeightsDto;
  readonly narrationApplied: boolean;
}

export interface ConciergeDto {
  readonly narrative: string;
  readonly narrationApplied: boolean;
  readonly recommendations: readonly RecommendationItemDto[];
  readonly audit: RecommendationAuditDto;
}

export interface ConciergeRequestDto {
  readonly text: string;
  readonly stayDate?: string;
}
