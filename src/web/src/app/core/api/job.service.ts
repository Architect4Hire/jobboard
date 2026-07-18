import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { Job, JobSummary, PostJobRequest } from '../models/job.model';
import { API_BASE_URL } from './api-base-url';

/**
 * Typed access to the Jobs endpoints behind the gateway (`/jobs`). Components use this, never HttpClient
 * directly. The auth interceptor attaches the JWT — no token handling here.
 */
@Injectable({ providedIn: 'root' })
export class JobService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/jobs`;

  /** GET /jobs — open jobs, optionally filtered to a category slug. */
  list(category?: string): Observable<readonly JobSummary[]> {
    const params = category ? new HttpParams().set('category', category) : undefined;
    return this.http.get<readonly JobSummary[]>(this.baseUrl, { params });
  }

  /** GET /jobs/{id}. */
  get(id: string): Observable<Job> {
    return this.http.get<Job>(`${this.baseUrl}/${id}`);
  }

  /** POST /jobs — post a new job. */
  post(body: PostJobRequest): Observable<Job> {
    return this.http.post<Job>(this.baseUrl, body);
  }

  /** POST /jobs/{id}/close — close an open job (publishes JobClosed server-side). */
  close(id: string): Observable<Job> {
    return this.http.post<Job>(`${this.baseUrl}/${id}/close`, null);
  }
}
