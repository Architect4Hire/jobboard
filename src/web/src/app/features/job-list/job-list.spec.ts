import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { JobService } from '../../core/api/job.service';
import { JobStatus, JobSummary } from '../../core/models/job.model';
import { JobList } from './job-list';

const SUMMARIES: readonly JobSummary[] = [
  {
    id: 'j1',
    title: 'Angular Engineer',
    location: 'Remote',
    salary: { min: 100000, max: 150000, currency: 'USD' },
    status: JobStatus.Open,
    categorySlugs: ['engineering'],
    createdOnUtc: '2026-01-01T00:00:00Z',
  },
  {
    id: 'j2',
    title: 'Product Designer',
    location: 'Berlin',
    salary: { min: 70000, max: 90000, currency: 'EUR' },
    status: JobStatus.Open,
    categorySlugs: ['design'],
    createdOnUtc: '2026-01-02T00:00:00Z',
  },
];

describe('JobList', () => {
  let list: ReturnType<typeof vi.fn>;

  function setup(summaries: readonly JobSummary[] = SUMMARIES) {
    list = vi.fn().mockReturnValue(of(summaries));
    TestBed.configureTestingModule({
      imports: [JobList],
      providers: [provideRouter([]), { provide: JobService, useValue: { list } }],
    });
    const fixture = TestBed.createComponent(JobList);
    fixture.detectChanges();
    return fixture;
  }

  it('fetches through JobService and renders a card per posting', () => {
    const fixture = setup();
    expect(list).toHaveBeenCalled();
    const titles = (fixture.nativeElement as HTMLElement).querySelectorAll('.card__title');
    expect([...titles].map((el) => el.textContent?.trim())).toEqual([
      'Angular Engineer',
      'Product Designer',
    ]);
  });

  it('filters cards to the selected category', () => {
    const fixture = setup();
    fixture.componentInstance['select']('design');
    fixture.detectChanges();

    const titles = (fixture.nativeElement as HTMLElement).querySelectorAll('.card__title');
    expect([...titles].map((el) => el.textContent?.trim())).toEqual(['Product Designer']);
  });

  it('shows an error message when the fetch fails', () => {
    list = vi.fn().mockReturnValue(throwError(() => new Error('down')));
    TestBed.configureTestingModule({
      imports: [JobList],
      providers: [provideRouter([]), { provide: JobService, useValue: { list } }],
    });
    const fixture = TestBed.createComponent(JobList);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.load-error')).not.toBeNull();
  });
});
