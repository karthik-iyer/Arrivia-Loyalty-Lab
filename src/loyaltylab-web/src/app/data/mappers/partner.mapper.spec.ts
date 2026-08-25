import { toPartnerThemeView } from './partner.mapper';
import type { PartnerThemeDto } from '../dto/partner.dto';

/** Captured GET /api/partners/current/theme for SUMMIT. */
const summitThemeJson = `{
  "code": "SUMMIT",
  "displayName": "Summit Rewards",
  "primaryColor": "#BE185D",
  "surfaceColor": "#FFF7ED",
  "accentColor": "#1D4ED8"
}`;

describe('partner mapper', () => {
  it('maps theme tokens from configuration (FR-X-04)', () => {
    const view = toPartnerThemeView(JSON.parse(summitThemeJson) as PartnerThemeDto);

    expect(view.displayName).toBe('Summit Rewards');
    expect(view.primaryColor).toBe('#BE185D');
    expect(view.logoUrl).toBeNull();
  });
});
