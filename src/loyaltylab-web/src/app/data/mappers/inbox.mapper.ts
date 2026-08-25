import type { NudgeView } from '../../domain';
import type { NudgeDto } from '../dto/inbox.dto';

export function toNudgeView(dto: NudgeDto): NudgeView {
  return {
    nudgeId: dto.nudgeId,
    propertyName: dto.propertyName,
    windowStart: dto.windowStart,
    windowEnd: dto.windowEnd,
    score: dto.score,
    signals: dto.signals.map((signal) => ({ kind: signal.kind, contribution: signal.contribution })),
    expiresAt: dto.expiresAt,
  };
}
