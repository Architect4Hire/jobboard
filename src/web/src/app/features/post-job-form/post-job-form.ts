import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { JobService } from '../../core/api/job.service';
import { Session } from '../../core/auth/session';
import { JobClassificationInput, PostJobRequest } from '../../core/models/job.model';

/**
 * Employer form to post a job. Builds a PostJobRequest and sends it through JobService (gateway `/jobs`);
 * the employer id comes from the Session (the JWT), not a field. On success it routes to the new
 * posting's detail. Categories/tags are entered comma-separated and turned into name+slug pairs.
 */
@Component({
  selector: 'app-post-job-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  templateUrl: './post-job-form.html',
  styleUrl: './post-job-form.css',
})
export class PostJobForm {
  private readonly fb = inject(FormBuilder);
  private readonly jobService = inject(JobService);
  private readonly session = inject(Session);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.required],
    location: ['', Validators.required],
    salaryMin: [0, [Validators.required, Validators.min(0)]],
    salaryMax: [0, [Validators.required, Validators.min(0)]],
    currency: ['USD', [Validators.required, Validators.maxLength(3)]],
    categories: [''],
    tags: [''],
  });

  protected submit(): void {
    const employerId = this.session.userId();
    if (this.form.invalid || !employerId || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: PostJobRequest = {
      title: value.title.trim(),
      description: value.description.trim(),
      location: value.location.trim(),
      salary: { min: value.salaryMin, max: value.salaryMax, currency: value.currency.trim() },
      employerId,
      categories: toClassifications(value.categories),
      tags: toClassifications(value.tags),
    };

    this.submitting.set(true);
    this.error.set(null);

    this.jobService
      .post(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (job) => void this.router.navigate(['/jobs', job.id]),
        error: () => {
          this.error.set('Could not post the job. Please check the details and try again.');
          this.submitting.set(false);
        },
      });
  }
}

/** Split a comma-separated field into JobClassificationInput name/slug pairs, dropping blanks. */
function toClassifications(raw: string): readonly JobClassificationInput[] {
  return raw
    .split(',')
    .map((part) => part.trim())
    .filter((name) => name.length > 0)
    .map((name) => ({ name, slug: slugify(name) }));
}

function slugify(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}
