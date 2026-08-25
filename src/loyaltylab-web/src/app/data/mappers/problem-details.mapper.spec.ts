import { HttpErrorResponse } from '@angular/common/http';

import { ProblemDetailsMapper } from './problem-details.mapper';

/** Captured RFC 7807 body from POST /api/offers/{id}/quote after expiry. */
const quoteExpiredJson = `{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The quote has expired; re-quote required.",
  "status": 409,
  "errorCode": "QUOTE_EXPIRED",
  "correlationId": "0HMTEST00000000000000000000"
}`;

describe('ProblemDetailsMapper', () => {
  const mapper = new ProblemDetailsMapper();

  it('reads errorCode and correlationId from the catalog extension members', () => {
    const error = new HttpErrorResponse({
      status: 409,
      statusText: 'Conflict',
      error: JSON.parse(quoteExpiredJson) as object,
      url: '/api/bookings',
    });

    const mapped = mapper.map(error);

    expect(mapped.errorCode).toBe('QUOTE_EXPIRED');
    expect(mapped.status).toBe(409);
    expect(mapped.correlationId).toBe('0HMTEST00000000000000000000');
    expect(mapped.message).toContain('expired');
  });

  it('falls back to UNEXPECTED when the body is not problem details', () => {
    const mapped = mapper.map(new Error('network down'));

    expect(mapped.errorCode).toBe('UNEXPECTED');
    expect(mapped.correlationId).toBeNull();
  });
});
