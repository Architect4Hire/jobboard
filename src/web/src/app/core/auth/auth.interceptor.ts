import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { API_BASE_URL } from '../api/api-base-url';
import { TokenStore } from './token-store';

/**
 * The one place the Identity JWT meets outgoing requests. It attaches `Authorization: Bearer …` only to
 * gateway calls (URLs under API_BASE_URL) that we hold a token for — so the JWT never leaks to a third-party
 * URL, and a token-less request (e.g. login itself) is left untouched. On a 401 for a request we *did*
 * authorize, the stored token is stale/rejected: clear it and route to login. A 401 from a request we
 * didn't authorize (a failed login) is left for the caller to surface — we don't wipe state or redirect.
 * Functional interceptor — registered via `provideHttpClient(withInterceptors([authInterceptor]))`.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const tokenStore = inject(TokenStore);
  const router = inject(Router);
  const baseUrl = inject(API_BASE_URL);

  const accessToken = tokenStore.accessToken;
  const isGatewayRequest = request.url.startsWith(baseUrl);
  const attach = accessToken !== null && isGatewayRequest;

  const outgoing = attach
    ? request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
    : request;

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      if (attach && error instanceof HttpErrorResponse && error.status === 401) {
        tokenStore.clear();
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    }),
  );
};
