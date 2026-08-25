import { applyPartnerTheme, isCssColor } from './theming';
import type { PartnerThemeView } from '../domain';

const summit: PartnerThemeView = {
  code: 'SUMMIT',
  displayName: 'Summit Rewards',
  primaryColor: '#BE185D',
  surfaceColor: '#FFF7ED',
  accentColor: '#1D4ED8',
  logoUrl: null,
};

describe('theming', () => {
  it('accepts #RRGGBB and rejects injection attempts', () => {
    expect(isCssColor('#BE185D')).toBe(true);
    expect(isCssColor('#fff7ed')).toBe(true);
    expect(isCssColor('red')).toBe(false);
    expect(isCssColor('#FFF')).toBe(false);
    expect(isCssColor('url(javascript:alert(1))')).toBe(false);
  });

  it('writes only valid tokens onto the root', () => {
    const root = document.createElement('div').style;
    applyPartnerTheme(
      { ...summit, primaryColor: 'red', surfaceColor: '#0F766E' },
      root,
    );

    expect(root.getPropertyValue('--ll-color-primary')).toBe('');
    expect(root.getPropertyValue('--ll-color-surface')).toBe('#0F766E');
  });
});
