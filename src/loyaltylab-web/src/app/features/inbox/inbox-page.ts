import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { InboxStore } from '../../application/inbox.store';
import type { NudgeSignalView, NudgeView } from '../../domain';

@Component({
  selector: 'll-inbox-page',
  imports: [RouterLink],
  templateUrl: './inbox-page.html',
  styleUrl: './inbox-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [InboxStore],
})
export class InboxPage implements OnInit {
  private readonly router = inject(Router);
  readonly store = inject(InboxStore);

  ngOnInit(): void {
    void this.store.load();
  }

  async onAction(nudgeId: string): Promise<void> {
    const quoteId = await this.store.action(nudgeId);
    if (quoteId) {
      await this.router.navigate(['/checkout', quoteId]);
    }
  }

  onDismiss(nudgeId: string): void {
    void this.store.dismiss(nudgeId);
  }

  windowLabel(nudge: NudgeView): string {
    return `${this.day(nudge.windowStart)} – ${this.day(nudge.windowEnd)}`;
  }

  scoreLabel(score: number): string {
    return `${Math.round(score * 100)}% fit`;
  }

  kindLabel(kind: string): string {
    return kind.replace(/([A-Z])/g, ' $1').trim();
  }

  percent(value: number): string {
    return `${Math.round(value * 100)}%`;
  }

  contributionLabel(signal: NudgeSignalView): string {
    return `${this.kindLabel(signal.kind)} · weight ${this.percent(signal.weight)} · ${this.percent(signal.contribution)} of score`;
  }

  private day(isoDate: string): string {
    const [year, month, day] = isoDate.split('-').map(Number);
    if (!year || !month || !day) {
      return isoDate;
    }

    return new Date(year, month - 1, day).toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
  }
}
