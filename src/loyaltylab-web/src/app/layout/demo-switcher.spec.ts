import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';

import { ok, PARTNER_PORT, type PartnerPort, type PartnerThemeView } from '../domain';
import { DEMO_PERSONAS } from '../core/demo-personas';
import { SessionStore } from '../core/session.store';
import { ThemeApplier } from '../core/theme-applier';
import { DemoSwitcher } from './demo-switcher';

const summit: PartnerThemeView = {
  code: 'SUMMIT',
  displayName: 'Summit Rewards',
  primaryColor: '#BE185D',
  surfaceColor: '#FFF7ED',
  accentColor: '#1D4ED8',
  logoUrl: null,
};

const nimbus: PartnerThemeView = {
  code: 'NIMBUS',
  displayName: 'Nimbus Club',
  primaryColor: '#0F766E',
  surfaceColor: '#F0FDFA',
  accentColor: '#134E4A',
  logoUrl: null,
};

describe('DemoSwitcher', () => {
  it('switching partner restyles without reload', async () => {
    const fake: PartnerPort = {
      theme: async () => {
        const partner = TestBed.inject(SessionStore).partnerCode();
        return ok(partner === 'NIMBUS' ? nimbus : summit);
      },
    };

    TestBed.configureTestingModule({
      imports: [DemoSwitcher],
      providers: [provideRouter([]), ThemeApplier, { provide: PARTNER_PORT, useValue: fake }],
    });

    TestBed.inject(ThemeApplier);
    const fixture = TestBed.createComponent(DemoSwitcher);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(document.documentElement.style.getPropertyValue('--ll-color-primary')).toBe('#BE185D');

    const nimbusChen = DEMO_PERSONAS.find((persona) => persona.id === 'nimbus-chen');
    expect(nimbusChen).toBeDefined();
    await fixture.componentInstance.onPersonaChange(nimbusChen?.id ?? '');
    fixture.detectChanges();

    expect(document.documentElement.style.getPropertyValue('--ll-color-primary')).toBe('#0F766E');
    expect(document.documentElement.style.getPropertyValue('--ll-color-surface')).toBe('#F0FDFA');
    expect(fixture.nativeElement.querySelector('.brand')?.textContent).toContain('Nimbus Club');
    expect(fixture.nativeElement.textContent).toContain('Offers');
    expect(fixture.nativeElement.textContent).toContain('Wallet');
  });
});
