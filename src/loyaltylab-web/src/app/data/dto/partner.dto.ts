export interface PartnerThemeDto {
  readonly code: string;
  readonly displayName: string;
  readonly primaryColor: string;
  readonly surfaceColor: string;
  readonly accentColor: string;
  readonly logoUrl?: string | null;
}
