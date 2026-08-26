import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { confirmedSagaItem, reviewSagaItem } from '../../application/test-fixtures';
import { ok, OPERATOR_PORT } from '../../domain';
import { OperatorListPage } from './operator-list-page';

describe('OperatorListPage', () => {
  async function render(): Promise<ComponentFixture<OperatorListPage>> {
    TestBed.configureTestingModule({
      imports: [OperatorListPage],
      providers: [
        provideRouter([]),
        {
          provide: OPERATOR_PORT,
          useValue: {
            listSagas: async () => ok([confirmedSagaItem, reviewSagaItem]),
            runWorker: async () => ok({ worker: 'scan', processed: 1 }),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(OperatorListPage);
    fixture.detectChanges();
    await fixture.componentInstance.store.load();
    fixture.detectChanges();
    return fixture;
  }

  it('lists review-needed sagas first', async () => {
    const fixture = await render();
    const rows = fixture.nativeElement.querySelectorAll('tbody tr') as NodeListOf<HTMLTableRowElement>;

    expect(rows[0]?.textContent).toContain('RequiresManualReview');
    expect(rows[0]?.className).toContain('row--review');
    expect(rows[1]?.textContent).toContain('Confirmed');
  });

  it('runs the opportunity scan from the operator list', async () => {
    const workers: string[] = [];
    TestBed.configureTestingModule({
      imports: [OperatorListPage],
      providers: [
        provideRouter([]),
        {
          provide: OPERATOR_PORT,
          useValue: {
            listSagas: async () => ok([confirmedSagaItem, reviewSagaItem]),
            runWorker: async (worker: string) => {
              workers.push(worker);
              return ok({ worker, processed: 1 });
            },
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(OperatorListPage);
    fixture.detectChanges();
    await fixture.componentInstance.store.load();
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.scan button') as HTMLButtonElement;
    button.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(workers).toEqual(['scan']);
    expect(fixture.nativeElement.textContent).toContain('Scanned 1 members.');
  });
});
