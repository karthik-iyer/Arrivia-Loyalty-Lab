import type { NudgeView, QuoteView } from '../../domain';
import type { ActionedNudgeDto, InboxDto, NudgeDto } from '../dto/inbox.dto';
import { toMoney } from './money.mapper';

export function toNudgeView(dto: NudgeDto): NudgeView {
  return {
    nudgeId: dto.nudgeId,
    offerId: dto.offerId,
    propertyName: dto.propertyName,
    windowStart: dto.windowStart,
    windowEnd: dto.windowEnd,
    score: dto.score,
    signals: dto.signals.map((signal) => ({
      kind: signal.kind,
      weight: signal.weight,
      contribution: signal.contribution,
    })),
    expiresAt: dto.expiresAt,
  };
}

export function toInboxNudges(dto: InboxDto): readonly NudgeView[] {
  return dto.nudges.map(toNudgeView);
}

export function toActionedQuote(dto: ActionedNudgeDto): QuoteView {
  return {
    quoteId: dto.quoteId,
    offerId: dto.offerId,
    memberPrice: toMoney(dto.memberPrice),
    maxCreditTender: toMoney(dto.maxCreditTender),
    maxCredits: dto.maxCredits,
    expiresAt: dto.expiresAt,
  };
}
