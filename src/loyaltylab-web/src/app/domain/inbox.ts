export interface NudgeSignalView {
  readonly kind: string;
  readonly weight: number;
  readonly contribution: number;
}

export interface NudgeView {
  readonly nudgeId: string;
  readonly offerId: string;
  readonly propertyName: string;
  readonly windowStart: string;
  readonly windowEnd: string;
  readonly score: number;
  readonly signals: readonly NudgeSignalView[];
  readonly expiresAt: string;
}
