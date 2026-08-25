import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { ThemeApplier } from './core/theme-applier';
import { DemoSwitcher } from './layout/demo-switcher';

@Component({
  selector: 'll-root',
  imports: [RouterOutlet, DemoSwitcher],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  constructor() {
    inject(ThemeApplier);
  }
}
