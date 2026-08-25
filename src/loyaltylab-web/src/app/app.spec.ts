import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';

import { App } from './app';
import { ThemeApplier } from './core/theme-applier';
import { ok, PARTNER_PORT } from './domain';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        ThemeApplier,
        {
          provide: PARTNER_PORT,
          useValue: {
            theme: async () =>
              ok({
                code: 'SUMMIT',
                displayName: 'Summit Rewards',
                primaryColor: '#BE185D',
                surfaceColor: '#FFF7ED',
                accentColor: '#1D4ED8',
                logoUrl: null,
              }),
          },
        },
      ],
    }).compileComponents();
  });

  it('should create the shell', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
