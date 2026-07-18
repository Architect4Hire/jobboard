import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';

import { authInterceptor } from '../auth/auth.interceptor';
import { API_BASE_URL } from './api-base-url';

/**
 * HTTP wiring for the app: an HttpClient with the auth interceptor, plus the gateway base path. Grouped
 * here so app.config.ts stays a one-line `provideAppHttp()`. `/api` is the same-origin proxy mount — the
 * dev-server proxy forwards it to the Aspire-injected gateway address (see proxy.conf.js); nothing points
 * at a service directly.
 */
export function provideAppHttp(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideHttpClient(withInterceptors([authInterceptor])),
    { provide: API_BASE_URL, useValue: '/api' },
  ]);
}
