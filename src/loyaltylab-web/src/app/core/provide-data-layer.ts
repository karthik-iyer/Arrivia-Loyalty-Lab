import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';

import { HttpBookingAdapter } from '../data/http/booking.adapter';
import { HttpCatalogAdapter } from '../data/http/catalog.adapter';
import { HttpConciergeAdapter } from '../data/http/concierge.adapter';
import { HttpResult } from '../data/http/http-result';
import { HttpInboxAdapter } from '../data/http/inbox.adapter';
import { HttpOperatorAdapter } from '../data/http/operator.adapter';
import { HttpPartnerAdapter } from '../data/http/partner.adapter';
import { HttpPricingAdapter } from '../data/http/pricing.adapter';
import { HttpWalletAdapter } from '../data/http/wallet.adapter';
import { ProblemDetailsMapper } from '../data/mappers/problem-details.mapper';
import {
  BOOKING_PORT,
  CATALOG_PORT,
  CONCIERGE_PORT,
  INBOX_PORT,
  OPERATOR_PORT,
  PARTNER_PORT,
  PRICING_PORT,
  WALLET_PORT,
} from '../domain';

/** Frontend composition root — binds every port to its HTTP adapter (ADR-0014). */
export function provideDataLayer(): EnvironmentProviders {
  return makeEnvironmentProviders([
    ProblemDetailsMapper,
    HttpResult,
    { provide: PARTNER_PORT, useClass: HttpPartnerAdapter },
    { provide: CATALOG_PORT, useClass: HttpCatalogAdapter },
    { provide: PRICING_PORT, useClass: HttpPricingAdapter },
    { provide: BOOKING_PORT, useClass: HttpBookingAdapter },
    { provide: WALLET_PORT, useClass: HttpWalletAdapter },
    { provide: CONCIERGE_PORT, useClass: HttpConciergeAdapter },
    { provide: INBOX_PORT, useClass: HttpInboxAdapter },
    { provide: OPERATOR_PORT, useClass: HttpOperatorAdapter },
  ]);
}
