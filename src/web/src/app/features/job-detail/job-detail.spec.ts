import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { ApplicationService } from '../../core/api/application.service';
import { JobService } from '../../core/api/job.service';
import { Session } from '../../core/auth/session';
import { Application, ApplicationStatus } from '../../core/models/application.model';
import { Job, JobStatus } from '../../core/models/job.model';
import { JobDetail } from './job-detail';

const JOB: Job = {
  id: 'j1',
  title: 'Angular Engineer',
  description: 'Build things.',
  location: 'Remote',
  salary: { min: 100000, max: 150000, currency: 'USD' },
  status: JobStatus.Open,
  employerId: 'emp-9',
  categories: [{ name: 'Engineering', slug: 'engineering' }],
  tags: [],
  createdOnUtc: '2026-01-01T00:00:00Z',
};

const SUBMITTED: Application = {
  id: 'a1',
  candidateId: 'cand-1',
  jobId: 'j1',
  status: ApplicationStatus.Submitted,
  resumeReference: null,
  submittedOnUtc: '2026-01-03T00:00:00Z',
  statusChangedOnUtc: '2026-01-03T00:00:00Z',
};

describe('JobDetail', () => {
  let get: ReturnType<typeof vi.fn>;
  let submit: ReturnType<typeof vi.fn>;

  function setup(session: Partial<Session>) {
    get = vi.fn().mockReturnValue(of(JOB));
    submit = vi.fn().mockReturnValue(of(SUBMITTED));
    TestBed.configureTestingModule({
      imports: [JobDetail],
      providers: [
        provideRouter([]),
        { provide: JobService, useValue: { get } },
        { provide: ApplicationService, useValue: { submit } },
        { provide: Session, useValue: session },
      ],
    });
    const fixture = TestBed.createComponent(JobDetail);
    fixture.componentRef.setInput('id', 'j1');
    fixture.detectChanges();
    return fixture;
  }

  const candidate: Partial<Session> = {
    isCandidate: (() => true) as Session['isCandidate'],
    isAuthenticated: (() => true) as Session['isAuthenticated'],
    userId: (() => 'cand-1') as Session['userId'],
  };

  it('loads the job through JobService and renders its title', () => {
    const fixture = setup(candidate);
    expect(get).toHaveBeenCalledWith('j1');
    expect((fixture.nativeElement as HTMLElement).querySelector('h1')?.textContent).toContain(
      'Angular Engineer',
    );
  });

  it('submits an application with the session candidate id when a candidate applies', () => {
    const fixture = setup(candidate);
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.apply__btn')!.click();
    fixture.detectChanges();

    expect(submit).toHaveBeenCalledWith({ candidateId: 'cand-1', jobId: 'j1', resumeReference: null });
    expect((fixture.nativeElement as HTMLElement).querySelector('.apply__done')).not.toBeNull();
  });

  it('offers no apply button to an anonymous visitor', () => {
    const fixture = setup({
      isCandidate: (() => false) as Session['isCandidate'],
      isAuthenticated: (() => false) as Session['isAuthenticated'],
      userId: (() => null) as Session['userId'],
    });
    expect((fixture.nativeElement as HTMLElement).querySelector('.apply__btn')).toBeNull();
  });
});
