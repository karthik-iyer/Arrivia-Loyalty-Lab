import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';

import { reviewNeededSaga } from '../../application/test-fixtures';
import { ok, OPERATOR_PORT } from '../../domain';
import { OperatorDetailPage } from './operator-detail-page';

describe('OperatorDetailPage', () => {
  async function render(): Promise<ComponentFixture<OperatorDetailPage>> {
    TestBed.configureTestingModule({
      imports: [OperatorDetailPage],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => reviewNeededSaga.id } } } },
        {
          provide: OPERATOR_PORT,
          useValue: { getSaga: async () => ok(reviewNeededSaga) },
        },
      ],
    });

    const fixture = TestBed.createComponent(OperatorDetailPage);
    fixture.detectChanges();
    await fixture.componentInstance.store.load(reviewNeededSaga.id);
    fixture.detectChanges();
    return fixture;
  }

  it('shows steps, compensation outcome, poison, and highlights the failing step', async () => {
    const fixture = await render();
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('RequiresManualReview');
    expect(text).toContain('PAYMENT_DECLINED');
    expect(text).toContain('Compensation Failed');
    expect(text).toContain('COMPENSATION_EXHAUSTED');
    expect(text).toContain('3 attempts');
    expect(text).toContain('410 ms');
    expect(text).toContain('AdvanceSaga');
    expect(text).toContain('TIMEOUT');
    expect(text).toContain('Last heartbeat');
    expect(fixture.nativeElement.querySelector('.step--review')).not.toBeNull();
  });
});
