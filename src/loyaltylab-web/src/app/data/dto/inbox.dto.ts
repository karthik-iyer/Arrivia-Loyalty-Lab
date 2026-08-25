export interface NudgeDto {
  readonly nudgeId: string;
  readonly propertyName: string;
  readonly windowStart: string;
  readonly windowEnd: string;
  readonly score: number;
  readonly signals: readonly { readonly kind: string; readonly contribution: number }[];
  readonly expiresAt: string;
}
