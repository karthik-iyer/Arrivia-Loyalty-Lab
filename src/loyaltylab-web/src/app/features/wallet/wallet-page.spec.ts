import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { mayaBalance, mayaStatement, originalBurnId } from '../../application/test-fixtures';
import { ok, WALLET_PORT } from '../../domain';
import { WalletPage } from './wallet-page';

describe('WalletPage', () => {
  async function render(): Promise<ComponentFixture<WalletPage>> {
    TestBed.configureTestingModule({
      imports: [WalletPage],
      providers: [
        provideRouter([]),
        {
          provide: WALLET_PORT,
          useValue: {
            balance: async () => ok(mayaBalance),
            statement: async () => ok(mayaStatement),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(WalletPage);
    fixture.detectChanges();
    await fixture.componentInstance.store.load();
    fixture.detectChanges();
    return fixture;
  }

  it('shows credits, reason, and running balance', async () => {
    const fixture = await render();
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('6000 credits');
    expect(text).toContain('$60.00');
    expect(text).toContain('Opening grant');
    expect(text).toContain('Booking tender');
    expect(text).toContain('1170');
  });

  it('links a reversal to the original transaction row', async () => {
    const fixture = await render();
    const original = fixture.nativeElement.querySelector(`#txn-${originalBurnId}`) as HTMLElement | null;
    const link = fixture.nativeElement.querySelector(
      `a[href="#txn-${originalBurnId}"]`,
    ) as HTMLAnchorElement | null;

    expect(original).not.toBeNull();
    expect(original?.textContent).toContain('Booking tender');
    expect(link).not.toBeNull();
    expect(link?.textContent).toContain('Reverses original');
  });
});
