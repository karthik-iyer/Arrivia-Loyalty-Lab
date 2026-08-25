import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { correlationInterceptor } from './core/correlation.interceptor';
import { provideDataLayer } from './core';
import { tenantInterceptor } from './core/tenant.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([tenantInterceptor, correlationInterceptor])),
    provideDataLayer(),
  ],
};
