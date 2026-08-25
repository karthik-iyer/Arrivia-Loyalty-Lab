export type AccessRole =
  | 'Anonymous'
  | 'Member'
  | 'AccountManager'
  | 'FinanceAnalyst'
  | 'Operator';

export interface PartnerThemeView {
  readonly code: string;
  readonly displayName: string;
  readonly primaryColor: string;
  readonly surfaceColor: string;
  readonly accentColor: string;
  readonly logoUrl: string | null;
}

export interface DemoIdentity {
  readonly id: string;
  readonly label: string;
  readonly partnerCode: string;
  readonly memberId: string | null;
  readonly role: AccessRole;
}
