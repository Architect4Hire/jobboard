import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';

import { JobService } from '../../core/api/job.service';
import { JobStatus, JobSummary } from '../../core/models/job.model';

/**
 * The job board: cards for every posting, filterable by category. Fetches the list once through
 * JobService (gateway `/jobs`) and filters client-side so the category options stay stable and switching
 * is instant. `toSignal` owns the subscription — no manual teardown.
 */
@Component({
  selector: 'app-job-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DecimalPipe],
  templateUrl: './job-list.html',
  styleUrl: './job-list.css',
})
export class JobList {
  private readonly jobService = inject(JobService);

  protected readonly loadError = signal(false);

  /** undefined while loading, then the full set of postings ([] if the fetch failed — see loadError). */
  private readonly jobs = toSignal(
    this.jobService.list().pipe(
      catchError(() => {
        this.loadError.set(true);
        return of<readonly JobSummary[]>([]);
      }),
    ),
  );

  protected readonly selectedCategory = signal<string | null>(null);

  /** Distinct category slugs across all postings, for the filter bar. */
  protected readonly categories = computed(() => {
    const jobs = this.jobs();
    return jobs ? [...new Set(jobs.flatMap((job) => job.categorySlugs))].sort() : [];
  });

  /** The postings to show — all, or those in the selected category. undefined while loading. */
  protected readonly visibleJobs = computed(() => {
    const jobs = this.jobs();
    if (!jobs) {
      return undefined;
    }
    const category = this.selectedCategory();
    return category ? jobs.filter((job) => job.categorySlugs.includes(category)) : jobs;
  });

  protected readonly JobStatus = JobStatus;

  protected select(category: string | null): void {
    this.selectedCategory.set(category);
  }
}
