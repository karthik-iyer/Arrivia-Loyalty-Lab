import { computed, inject, Injectable, signal } from '@angular/core';

import { PARTNER_PORT, type DemoIdentity, type PartnerThemeView } from '../domain';
import { DEFAULT_PERSONA } from './demo-personas';

@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly partners = inject(PARTNER_PORT);
  private generation = 0;

  private readonly _identity = signal<DemoIdentity>(DEFAULT_PERSONA);
  private readonly _theme = signal<PartnerThemeView | null>(null);
  private readonly _correlationId = signal<string | null>(null);

  readonly identity = this._identity.asReadonly();
  readonly theme = this._theme.asReadonly();
  readonly correlationId = this._correlationId.asReadonly();
  readonly partnerCode = computed(() => this._identity().partnerCode);

  recordCorrelation(id: string): void {
    this._correlationId.set(id);
  }

  async selectPersona(persona: DemoIdentity): Promise<void> {
    this._identity.set(persona);
    await this.refreshTheme();
  }

  async refreshTheme(): Promise<void> {
    const ticket = ++this.generation;
    const result = await this.partners.theme();
    if (ticket !== this.generation) {
      return;
    }

    if (result.ok) {
      this._theme.set(result.value);
    }
  }
}
