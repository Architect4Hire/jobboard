import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

/**
 * The app's routes. Everything is lazily loaded; the two employer/candidate pages are gated by authGuard
 * (redirects to /login when signed out). All data still flows through the typed services to the gateway —
 * routing only decides which screen is shown.
 */
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/job-list/job-list').then((m) => m.JobList),
  },
  {
    path: 'jobs',
    loadComponent: () => import('./features/job-list/job-list').then((m) => m.JobList),
  },
  {
    path: 'jobs/new',
    canActivate: [authGuard],
    loadComponent: () => import('./features/post-job-form/post-job-form').then((m) => m.PostJobForm),
  },
  {
    path: 'jobs/:id',
    loadComponent: () => import('./features/job-detail/job-detail').then((m) => m.JobDetail),
  },
  {
    path: 'applications',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/application-list/application-list').then((m) => m.ApplicationList),
  },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/register/register').then((m) => m.Register),
  },
  { path: '**', redirectTo: '' },
];
