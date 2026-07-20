import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { Session } from './session';

/**
 * Gate for employer-only routes (post a job). Signed-out users go to /login like authGuard; a signed-in
 * non-employer (a candidate) is sent to the job list rather than shown a form they can't use. This only
 * drives what the UI offers — the Jobs service still validates the JWT on POST /jobs. Functional guard;
 * use as `canActivate: [employerGuard]`.
 */
export const employerGuard: CanActivateFn = () => {
  const session = inject(Session);
  const router = inject(Router);

  if (!session.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  return session.isEmployer() ? true : router.createUrlTree(['/jobs']);
};
