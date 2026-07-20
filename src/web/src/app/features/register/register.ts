import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';

import { AuthService } from '../../core/api/auth.service';
import { AccountRole } from '../../core/models/identity.model';

/**
 * Create an account, then sign in. Registers through AuthService (gateway `/identity/register`) and, on
 * success, logs the new account in for a token before routing home — register alone returns no JWT.
 */
@Component({
  selector: 'app-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.css',
})
export class Register {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly AccountRole = AccountRole;

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    role: [AccountRole.Candidate, Validators.required],
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password, role } = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);

    this.auth
      .register({ email, password, role })
      .pipe(
        switchMap(() => this.auth.login({ email, password })),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => void this.router.navigate(['/']),
        error: () => {
          this.error.set('Could not create the account. The email may already be in use.');
          this.submitting.set(false);
        },
      });
  }
}
