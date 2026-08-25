import { effect, inject, Injectable } from '@angular/core';

import { SessionStore } from './session.store';
import { applyPartnerTheme } from './theming';

@Injectable({ providedIn: 'root' })
export class ThemeApplier {
  private readonly session = inject(SessionStore);

  constructor() {
    effect(() => {
      applyPartnerTheme(this.session.theme(), document.documentElement.style);
    });
  }
}
