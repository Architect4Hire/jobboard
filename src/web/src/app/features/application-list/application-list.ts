import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { ApplicationService } from '../../core/api/application.service';
import { ApplicationHistoryItem, ApplicationStatus } from '../../core/models/application.model';
import { ApplicationStatusBadge } from '../application-status/application-status';

/**
 * A candidate's own applications with their live status, job title, and employer name. Lists through
 * ApplicationService (gateway `/applications/mine`) — a materialized read-model projection Applications
 * keeps current off the bus (ADR-0012), not a fan-out to other services; the candidate is derived
 * server-side from the JWT, never passed from here. Withdrawing updates just the affected row in place.
 * This route is guarded, so a signed-in candidate is assumed.
 */
@Component({
  selector: 'app-application-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, ApplicationStatusBadge],
  templateUrl: './application-list.html',
  styleUrl: './application-list.css',
})
export class ApplicationList {
  private readonly applicationService = inject(ApplicationService);
  private readonly destroyRef = inject(DestroyRef);

  /** undefined while loading, then the candidate's applications. */
  protected readonly applications = signal<readonly ApplicationHistoryItem[] | undefined>(undefined);

  protected readonly loadError = signal(false);

  /** The id of the application currently being withdrawn, or null — guards against double submits. */
  protected readonly withdrawing = signal<string | null>(null);

  protected readonly ApplicationStatus = ApplicationStatus;

  constructor() {
    this.applicationService
      .listMine()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (applications) => this.applications.set(applications),
        error: () => {
          this.loadError.set(true);
          this.applications.set([]);
        },
      });
  }

  /** True while the application can still be withdrawn (not in a terminal state). */
  protected canWithdraw(status: ApplicationStatus): boolean {
    return status === ApplicationStatus.Submitted || status === ApplicationStatus.Reviewed;
  }

  protected withdraw(id: string): void {
    if (this.withdrawing()) {
      return;
    }
    this.withdrawing.set(id);

    this.applicationService
      .withdraw(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.applications.update((list) =>
            list?.map((application) =>
              application.id === updated.id
                ? {
                    ...application,
                    status: updated.status,
                    statusChangedOnUtc: updated.statusChangedOnUtc,
                  }
                : application,
            ),
          );
          this.withdrawing.set(null);
        },
        error: () => this.withdrawing.set(null),
      });
  }
}
