import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { clampedExplanation } from '../../application/test-fixtures';
import { ExplanationPanel } from './explanation-panel';

@Component({
  imports: [ExplanationPanel],
  template: '<ll-explanation-panel [explanation]="explanation" />',
})
class Host {
  explanation = clampedExplanation;
}

describe('ExplanationPanel', () => {
  it('marks a clamped stage as visually distinct', () => {
    TestBed.configureTestingModule({ imports: [Host] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    const clamped = fixture.nativeElement.querySelector('.stage--clamped') as HTMLElement | null;
    expect(clamped).not.toBeNull();
    expect(clamped?.classList.contains('stage--clamped')).toBe(true);
    expect(clamped?.textContent).toContain('Raised by 0.92');
  });
});
