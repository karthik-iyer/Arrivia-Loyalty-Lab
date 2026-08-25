/** Typed failure from the catalog in docs/04 §9. Rendered, not thrown. */
export interface AppError {
  readonly errorCode: string;
  readonly message: string;
  readonly status: number;
  readonly correlationId: string | null;
}
