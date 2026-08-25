import { Component, inject } from '@angular/core';

import { DEMO_PERSONAS } from '../core/demo-personas';
import { SessionStore } from '../core/session.store';

@Component({
  selector: 'll-demo-switcher',
  templateUrl: './demo-switcher.html',
  styleUrl: './demo-switcher.scss',
})
export class DemoSwitcher {
  private readonly session = inject(SessionStore);

  readonly personas = DEMO_PERSONAS;
  readonly identity = this.session.identity;
  readonly theme = this.session.theme;
  readonly correlationId = this.session.correlationId;

  constructor() {
    void this.session.refreshTheme();
  }

  onSelect(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      void this.onPersonaChange(target.value);
    }
  }

  async onPersonaChange(id: string): Promise<void> {
    const persona = DEMO_PERSONAS.find((item) => item.id === id);
    if (persona) {
      await this.session.selectPersona(persona);
    }
  }
}
