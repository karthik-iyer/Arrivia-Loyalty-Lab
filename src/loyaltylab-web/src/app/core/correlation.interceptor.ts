import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap } from 'rxjs';

import { SessionStore } from './session.store';

export const correlationHeader = 'X-Correlation-Id';

export const correlationInterceptor: HttpInterceptorFn = (req, next) => {
  const session = inject(SessionStore);
  const id = req.headers.get(correlationHeader) ?? crypto.randomUUID();
  session.recordCorrelation(id);

  return next(req.clone({ headers: req.headers.set(correlationHeader, id) })).pipe(
    tap((event) => {
      if (event instanceof HttpResponse) {
        const echoed = event.headers.get(correlationHeader);
        if (echoed) {
          session.recordCorrelation(echoed);
        }
      }
    }),
  );
};
