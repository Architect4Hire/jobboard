import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from './core/api/auth.service';
import { Session } from './core/auth/session';

/**
 * App shell: a nav bar whose actions follow the Session (employers see "Post a job", candidates see
 * "My applications", signed-out users see "Log in") over a router outlet. Signing out clears the token
 * via AuthService and returns home.
 */
@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly session = inject(Session);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly isAuthenticated = this.session.isAuthenticated;
  protected readonly isEmployer = this.session.isEmployer;
  protected readonly isCandidate = this.session.isCandidate;

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/']);
  }
}
