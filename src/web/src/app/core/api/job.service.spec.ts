import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { Job, JobStatus, JobSummary } from '../models/job.model';
import { API_BASE_URL } from './api-base-url';
import { JobService } from './job.service';

describe('JobService', () => {
  let service: JobService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    service = TestBed.inject(JobService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GET /api/jobs and returns typed summaries', () => {
    const summaries: readonly JobSummary[] = [
      {
        id: 'a1',
        title: 'Engineer',
        location: 'Remote',
        salary: { min: 100, max: 200, currency: 'USD' },
        status: JobStatus.Open,
        categorySlugs: ['eng'],
        createdOnUtc: '2026-01-01T00:00:00Z',
      },
    ];

    let received: readonly JobSummary[] | undefined;
    service.list().subscribe((jobs) => (received = jobs));

    const req = httpMock.expectOne('/api/jobs');
    expect(req.request.method).toBe('GET');
    req.flush(summaries);
    expect(received).toEqual(summaries);
  });

  it('passes the category filter as a query param', () => {
    service.list('eng').subscribe();

    const req = httpMock.expectOne((r) => r.url === '/api/jobs');
    expect(req.request.params.get('category')).toBe('eng');
    req.flush([]);
  });

  it('POST /api/jobs/{id}/close returns the closed job', () => {
    const closed = { id: 'a1', status: JobStatus.Closed } as Job;

    let received: Job | undefined;
    service.close('a1').subscribe((job) => (received = job));

    const req = httpMock.expectOne('/api/jobs/a1/close');
    expect(req.request.method).toBe('POST');
    req.flush(closed);
    expect(received?.status).toBe(JobStatus.Closed);
  });
});
