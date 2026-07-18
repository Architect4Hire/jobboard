import { DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, EMPTY, switchMap } from 'rxjs';

import { ApplicationService } from '../../core/api/application.service';
import { JobService } from '../../core/api/job.service';
import { Application } from '../../core/models/application.model';
import { JobStatus } from '../../core/models/job.model';
import { Session } from '../../core/auth/session';
import { ApplicationStatusBadge } from '../application-status/application-status';

/**
 * A single posting plus the candidate apply action. Reads the job through JobService and submits through
 * ApplicationService — two typed services, each hitting the gateway; the candidate id comes from the
 * Session (the JWT), never from another service. Applying is offered only to a signed-in candidate on an
 * open posting.
 */
@Component({
  selector: 'app-job-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DecimalPipe, ApplicationStatusBadge],
  templateUrl: './job-detail.html',
  styleUrl: './job-detail.css',
})
export class JobDetail {
  private readonly jobService = inject(JobService);
  private readonly applicationService = inject(ApplicationService);
  private readonly session = inject(Session);
  private readonly destroyRef = inject(DestroyRef);

  /** Route param `/jobs/:id`, bound via the router's component input binding. */
  readonly id = input.required<string>();

  protected readonly loadError = signal(false);

  /** undefined while loading (or if the fetch failed — see loadError), then the full job. */
  protected readonly job = toSignal(
    toObservable(this.id).pipe(
      switchMap((id) => {
        this.loadError.set(false);
        return this.jobService.get(id).pipe(
          catchError(() => {
            this.loadError.set(true);
            return EMPTY;
          }),
        );
      }),
    ),
  );

  protected readonly isCandidate = this.session.isCandidate;
  protected readonly isAuthenticated = this.session.isAuthenticated;

  protected readonly applying = signal(false);
  protected readonly applyError = signal<string | null>(null);
  protected readonly submitted = signal<Application | null>(null);

  protected readonly JobStatus = JobStatus;

  protected apply(): void {
    const candidateId = this.session.userId();
    const job = this.job();
    if (!candidateId || !job || this.applying()) {
      return;
    }

    this.applying.set(true);
    this.applyError.set(null);

    this.applicationService
      .submit({ candidateId, jobId: job.id, resumeReference: null })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (application) => {
          this.submitted.set(application);
          this.applying.set(false);
        },
        error: () => {
          this.applyError.set('Could not submit your application. Please try again.');
          this.applying.set(false);
        },
      });
  }

  /** A one/two-letter monogram from the posting title, for the header badge. */
  protected monogram(title: string): string {
    const words = title.split(/\s+/).filter(Boolean);
    const letters = (words[0]?.[0] ?? '') + (words[1]?.[0] ?? '');
    return letters.toUpperCase() || '·';
  }
}
