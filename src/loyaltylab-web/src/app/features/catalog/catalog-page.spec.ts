import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { CATALOG_PORT, ok } from '../../domain';
import { anonymousCoral, coralOffer } from '../../application/test-fixtures';
import { CatalogPage } from './catalog-page';

describe('CatalogPage', () => {
  async function render(offers = [coralOffer]): Promise<ComponentFixture<CatalogPage>> {
    TestBed.configureTestingModule({
      imports: [CatalogPage],
      providers: [
        provideRouter([]),
        { provide: CATALOG_PORT, useValue: { search: async () => ok(offers) } },
      ],
    });

    const fixture = TestBed.createComponent(CatalogPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('renders the member price from the port, not from HTTP', async () => {
    const fixture = await render();
    expect(fixture.nativeElement.textContent).toContain('Coral Bay Resort');
    expect(fixture.nativeElement.textContent).toContain('$120.75');
  });

  it('states that a signed-out caller cannot see a member price', async () => {
    const fixture = await render([anonymousCoral]);
    expect(fixture.nativeElement.textContent).toContain('Sign in to see member price');
  });
});
