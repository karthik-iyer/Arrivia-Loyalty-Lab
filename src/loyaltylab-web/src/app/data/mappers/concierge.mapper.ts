import type { ConciergeRequest, ConciergeView } from '../../domain';
import type { ConciergeDto, ConciergeRequestDto } from '../dto/concierge.dto';
import { toMoney } from './money.mapper';

export function toConciergeRequestDto(request: ConciergeRequest): ConciergeRequestDto {
  return {
    text: request.text,
    ...(request.stayDate === undefined ? {} : { stayDate: request.stayDate }),
  };
}

export function toConciergeView(dto: ConciergeDto): ConciergeView {
  return {
    narrative: dto.narrative,
    narrationApplied: dto.narrationApplied,
    recommendations: dto.recommendations.map((item) => ({
      offerId: item.offerId,
      propertyName: item.propertyName,
      quoteId: item.quoteId,
      memberPrice: toMoney(item.memberPrice),
      creditsCover: item.creditsCover,
      score: item.score,
      reasons: item.reasons,
    })),
    audit: {
      candidatesConsidered: dto.audit.candidatesConsidered,
      candidatesReturned: dto.audit.candidatesReturned,
      interpretedTerms: dto.audit.interpretedTerms,
      exclusions: dto.audit.exclusions.map((exclusion) => ({
        offerId: exclusion.offerId,
        reason: exclusion.reason,
        detail: exclusion.detail,
      })),
      weights: { ...dto.audit.weights },
      narrationApplied: dto.audit.narrationApplied,
    },
  };
}
