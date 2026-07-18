import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { Session } from './session';

/**
 * Gate for routes that need a signed-in user (post a job, my applications). Mirrors the interceptor's
 * 401 behavior — no session, no page — by redirecting to /login instead of letting a component load and
 * fail its first authorized call. Functional guard; use as `canActivate: [authGuard]`.
 */
export const authGuard: CanActivateFn = () => {
  const session = inject(Session);
  const router = inject(Router);

  return session.isAuthenticated() ? true : router.createUrlTree(['/login']);
};
