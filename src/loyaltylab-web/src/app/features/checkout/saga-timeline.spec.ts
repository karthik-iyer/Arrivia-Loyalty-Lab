import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { compensatedBooking } from '../../application/test-fixtures';
import { SagaTimeline } from './saga-timeline';

@Component({
  imports: [SagaTimeline],
  template: '<ll-saga-timeline [steps]="steps" />',
})
class Host {
  steps = compensatedBooking.saga.steps;
}

describe('SagaTimeline', () => {
  it('marks compensated steps distinctly', () => {
    TestBed.configureTestingModule({ imports: [Host] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    const compensated = fixture.nativeElement.querySelector('.step--compensated') as HTMLElement | null;
    expect(compensated).not.toBeNull();
    expect(compensated?.textContent).toContain('Compensation Succeeded');
  });
});
