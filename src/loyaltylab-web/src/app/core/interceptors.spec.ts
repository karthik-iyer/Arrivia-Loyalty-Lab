import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ok, PARTNER_PORT } from '../domain';
import { correlationHeader, correlationInterceptor } from './correlation.interceptor';
import { DEMO_PERSONAS } from './demo-personas';
import { SessionStore } from './session.store';
import { memberHeader, partnerHeader, roleHeader, tenantInterceptor } from './tenant.interceptor';

function persona(id: string) {
  const found = DEMO_PERSONAS.find((item) => item.id === id);
  if (!found) {
    throw new Error(`Unknown persona ${id}`);
  }
  return found;
}

describe('HTTP interceptors', () => {
  let http: HttpClient;
  let httpTesting: HttpTestingController;
  let session: SessionStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tenantInterceptor, correlationInterceptor])),
        provideHttpClientTesting(),
        {
          provide: PARTNER_PORT,
          useValue: {
            theme: async () =>
              ok({
                code: 'SUMMIT',
                displayName: 'Summit Rewards',
                primaryColor: '#BE185D',
                surfaceColor: '#FFF7ED',
                accentColor: '#1D4ED8',
                logoUrl: null,
              }),
          },
        },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionStore);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('attaches partner and member headers from the session', async () => {
    await session.selectPersona(persona('nimbus-chen'));

    http.get('/api/offers').subscribe();
    const req = httpTesting.expectOne('/api/offers');

    expect(req.request.headers.get(partnerHeader)).toBe('NIMBUS');
    expect(req.request.headers.get(memberHeader)).toBe(persona('nimbus-chen').memberId);
    expect(req.request.headers.has(roleHeader)).toBe(false);
    req.flush([]);
  });

  it('attaches an internal role without fabricating a member', async () => {
    await session.selectPersona(persona('summit-operator'));

    http.get('/api/operator/sagas').subscribe();
    const req = httpTesting.expectOne('/api/operator/sagas');

    expect(req.request.headers.get(partnerHeader)).toBe('SUMMIT');
    expect(req.request.headers.has(memberHeader)).toBe(false);
    expect(req.request.headers.get(roleHeader)).toBe('Operator');
    req.flush([]);
  });

  it('sends a correlation id and records the echoed value', () => {
    http.get('/api/offers').subscribe();
    const req = httpTesting.expectOne('/api/offers');
    const sent = req.request.headers.get(correlationHeader);

    expect(sent).toBeTruthy();
    req.flush([], { headers: { [correlationHeader]: 'echoed-id' } });
    expect(session.correlationId()).toBe('echoed-id');
  });
});
