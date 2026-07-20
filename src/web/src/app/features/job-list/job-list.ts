import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';

import { JobService } from '../../core/api/job.service';
import { JobStatus, JobSummary } from '../../core/models/job.model';

/**
 * The job board: cards for every posting, filterable by category and free-text search. Fetches the list
 * once through JobService (gateway `/jobs`) and filters client-side so the category options stay stable
 * and switching is instant. `toSignal` owns the subscription — no manual teardown.
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
  protected readonly query = signal('');

  /** Distinct category slugs across all postings, for the filter bar. */
  protected readonly categories = computed(() => {
    const jobs = this.jobs();
    return jobs ? [...new Set(jobs.flatMap((job) => job.categorySlugs))].sort() : [];
  });

  /** The postings to show — filtered by the selected category and the search text. undefined while loading. */
  protected readonly visibleJobs = computed(() => {
    const jobs = this.jobs();
    if (!jobs) {
      return undefined;
    }
    const category = this.selectedCategory();
    const term = this.query().trim().toLowerCase();
    return jobs.filter((job) => {
      const inCategory = !category || job.categorySlugs.includes(category);
      const matches =
        !term ||
        job.title.toLowerCase().includes(term) ||
        job.location.toLowerCase().includes(term) ||
        job.categorySlugs.some((slug) => slug.includes(term));
      return inCategory && matches;
    });
  });

  /** Count of open postings across the whole board (not just the current filter). */
  protected readonly openCount = computed(
    () => this.jobs()?.filter((job) => job.status === JobStatus.Open).length ?? 0,
  );

  protected readonly JobStatus = JobStatus;

  protected select(category: string | null): void {
    this.selectedCategory.set(category);
  }

  protected onSearch(value: string): void {
    this.query.set(value);
  }

  /** A one/two-letter monogram from the posting title, for the card's badge. */
  protected monogram(title: string): string {
    const words = title.split(/\s+/).filter(Boolean);
    const letters = (words[0]?.[0] ?? '') + (words[1]?.[0] ?? '');
    return letters.toUpperCase() || '·';
  }

  /** True when the posting is remote-friendly (drives the little "Remote" pill). */
  protected isRemote(location: string): boolean {
    return /remote|anywhere/i.test(location);
  }

  /** Human "posted N days/weeks ago" from an ISO timestamp. */
  protected postedLabel(iso: string): string {
    const then = new Date(iso).getTime();
    if (Number.isNaN(then)) {
      return '';
    }
    const days = Math.max(0, Math.round((Date.now() - then) / 86_400_000));
    if (days === 0) return 'Posted today';
    if (days === 1) return 'Posted 1 day ago';
    if (days < 7) return `Posted ${days} days ago`;
    const weeks = Math.round(days / 7);
    return weeks === 1 ? 'Posted 1 week ago' : `Posted ${weeks} weeks ago`;
  }
}
