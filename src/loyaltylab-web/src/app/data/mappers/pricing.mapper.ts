import type { PriceExplanationView, PriceTraceEntry, QuoteView } from '../../domain';
import type { ExplainDto, QuoteDto, TraceDto } from '../dto/pricing.dto';
import { toMoney, toMoneyOrNull } from './money.mapper';

export function toQuoteView(dto: QuoteDto): QuoteView {
  return {
    quoteId: dto.quoteId,
    offerId: dto.offerId,
    memberPrice: toMoney(dto.memberPrice),
    maxCreditTender: toMoney(dto.maxCreditTender),
    maxCredits: dto.maxCredits,
    expiresAt: dto.expiresAt,
  };
}

export function toPriceTraceEntry(dto: TraceDto): PriceTraceEntry {
  return {
    stage: dto.stage,
    order: dto.order,
    description: dto.description,
    appliedRule: dto.appliedRule ?? null,
    subtotalBefore: toMoney(dto.subtotalBefore),
    subtotalAfter: toMoney(dto.subtotalAfter),
    wasClamped: dto.wasClamped,
    clampReason: dto.clampReason ?? null,
  };
}

export function toPriceExplanationView(dto: ExplainDto): PriceExplanationView {
  return {
    stages: dto.stages.map(toPriceTraceEntry),
    memberPrice: toMoney(dto.memberPrice),
    maxCreditTender: toMoneyOrNull(dto.maxCreditTender),
    netCost: toMoneyOrNull(dto.netCost),
    margin: toMoneyOrNull(dto.margin),
  };
}
