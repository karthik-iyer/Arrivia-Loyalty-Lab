import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { SessionStore } from './session.store';

export const partnerHeader = 'X-Partner-Code';
export const memberHeader = 'X-Member-Id';
export const roleHeader = 'X-Access-Role';

export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  const identity = inject(SessionStore).identity();
  let headers = req.headers.set(partnerHeader, identity.partnerCode);

  if (identity.memberId) {
    headers = headers.set(memberHeader, identity.memberId);
  }

  if (identity.role !== 'Anonymous' && identity.role !== 'Member') {
    headers = headers.set(roleHeader, identity.role);
  }

  return next(req.clone({ headers }));
};
