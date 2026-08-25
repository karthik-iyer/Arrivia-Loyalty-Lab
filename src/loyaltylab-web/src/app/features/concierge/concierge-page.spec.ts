import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { coralConcierge } from '../../application/test-fixtures';
import { CONCIERGE_PORT, ok } from '../../domain';
import { ConciergePage } from './concierge-page';

describe('ConciergePage', () => {
  async function render(): Promise<ComponentFixture<ConciergePage>> {
    await TestBed.configureTestingModule({
      imports: [ConciergePage],
      providers: [
        provideRouter([]),
        {
          provide: CONCIERGE_PORT,
          useValue: {
            recommend: async () => ok(coralConcierge),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ConciergePage);
    fixture.detectChanges();
    await fixture.componentInstance.store.search();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('shows the narrative, quote, and a collapsed audit from fake ports', async () => {
    const fixture = await render();
    const root = fixture.nativeElement as HTMLElement;
    const text = root.textContent ?? '';
    const audit = root.querySelector('details') as HTMLDetailsElement | null;

    expect(text).toContain('Coral Bay Resort fits your dates');
    expect(text).toContain('$120.75');
    expect(text).toContain('4830 credits');
    expect(text).toContain('Checkout');
    expect(audit).not.toBeNull();
    expect(audit?.open).toBe(false);
    expect(audit?.textContent).toContain('Why these results');
    expect(audit?.textContent).toContain('Unaffordable With Credits');
    expect(audit?.textContent).toContain('Requires 10416 credits, available 6000.');
  });
});
