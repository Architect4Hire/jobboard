import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';

import { JobService } from '../../core/api/job.service';
import { Session } from '../../core/auth/session';
import { Job, JobStatus } from '../../core/models/job.model';
import { PostJobForm } from './post-job-form';

const POSTED: Job = {
  id: 'new-job',
  title: 'Angular Engineer',
  description: 'Build things.',
  location: 'Remote',
  salary: { min: 100, max: 200, currency: 'USD' },
  status: JobStatus.Open,
  employerId: 'emp-1',
  categories: [],
  tags: [],
  createdOnUtc: '2026-01-01T00:00:00Z',
};

describe('PostJobForm', () => {
  let post: ReturnType<typeof vi.fn>;
  let navigate: ReturnType<typeof vi.fn>;

  function setup() {
    post = vi.fn().mockReturnValue(of(POSTED));
    navigate = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({
      imports: [PostJobForm],
      providers: [
        { provide: JobService, useValue: { post } },
        { provide: Router, useValue: { navigate } },
        { provide: Session, useValue: { userId: (() => 'emp-1') as Session['userId'] } },
      ],
    });
    const fixture = TestBed.createComponent(PostJobForm);
    fixture.detectChanges();
    return fixture;
  }

  it('posts the employer id from the session and the parsed classifications, then routes to the job', () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    component['form'].setValue({
      title: 'Angular Engineer',
      description: 'Build things.',
      location: 'Remote',
      salaryMin: 100,
      salaryMax: 200,
      currency: 'USD',
      categories: 'Engineering, Remote',
      tags: 'Angular',
    });

    component['submit']();

    expect(post).toHaveBeenCalledWith({
      title: 'Angular Engineer',
      description: 'Build things.',
      location: 'Remote',
      salary: { min: 100, max: 200, currency: 'USD' },
      employerId: 'emp-1',
      categories: [
        { name: 'Engineering', slug: 'engineering' },
        { name: 'Remote', slug: 'remote' },
      ],
      tags: [{ name: 'Angular', slug: 'angular' }],
    });
    expect(navigate).toHaveBeenCalledWith(['/jobs', 'new-job']);
  });

  it('does not post an invalid form', () => {
    const fixture = setup();
    fixture.componentInstance['submit']();
    expect(post).not.toHaveBeenCalled();
  });
});
