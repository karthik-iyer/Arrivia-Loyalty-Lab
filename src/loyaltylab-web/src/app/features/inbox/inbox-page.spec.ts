import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { coralNudge } from '../../application/test-fixtures';
import { INBOX_PORT, ok } from '../../domain';
import { InboxPage } from './inbox-page';

describe('InboxPage', () => {
  async function render(): Promise<ComponentFixture<InboxPage>> {
    await TestBed.configureTestingModule({
      imports: [InboxPage],
      providers: [
        provideRouter([]),
        {
          provide: INBOX_PORT,
          useValue: {
            list: async () => ok([coralNudge]),
            action: async () =>
              ok({
                quoteId: 'q-live',
                offerId: coralNudge.offerId,
                memberPrice: { amount: 120.75, currency: 'USD' },
                maxCreditTender: { amount: 48.3, currency: 'USD' },
                maxCredits: 4830,
                expiresAt: '2026-03-15T12:15:00+00:00',
              }),
            dismiss: async () => ok(undefined),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(InboxPage);
    fixture.detectChanges();
    await fixture.componentInstance.store.load();
    fixture.detectChanges();
    return fixture;
  }

  it('shows the offer, window, and a collapsed why-am-I-seeing-this breakdown', async () => {
    const fixture = await render();
    const root = fixture.nativeElement as HTMLElement;
    const text = root.textContent ?? '';
    const why = root.querySelector('details') as HTMLDetailsElement | null;

    expect(text).toContain('Coral Bay Resort');
    expect(text).toContain('29 Mar 2026');
    expect(text).toContain('12 Apr 2026');
    expect(text).toContain('68% fit');
    expect(text).toContain('Book this stay');
    expect(text).toContain('Dismiss');
    expect(why).not.toBeNull();
    expect(why?.open).toBe(false);
    expect(why?.textContent).toContain('Why am I seeing this?');
    expect(why?.textContent).toContain('Window Fit');
    expect(why?.textContent).toContain('weight 20%');
  });
});
