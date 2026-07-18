import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import {
  AdvanceApplicationStatusRequest,
  Application,
  ApplicationSummary,
  SubmitApplicationRequest,
} from '../models/application.model';
import { API_BASE_URL } from './api-base-url';

/**
 * Typed access to the Applications endpoints behind the gateway (`/applications`, a protected route). The
 * auth interceptor supplies the JWT the gateway requires.
 */
@Injectable({ providedIn: 'root' })
export class ApplicationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/applications`;

  /** GET /applications?candidateId= — a candidate's applications. */
  listByCandidate(candidateId: string): Observable<readonly ApplicationSummary[]> {
    const params = new HttpParams().set('candidateId', candidateId);
    return this.http.get<readonly ApplicationSummary[]>(this.baseUrl, { params });
  }

  /** GET /applications/{id}. */
  get(id: string): Observable<Application> {
    return this.http.get<Application>(`${this.baseUrl}/${id}`);
  }

  /** POST /applications — submit an application. */
  submit(body: SubmitApplicationRequest): Observable<Application> {
    return this.http.post<Application>(this.baseUrl, body);
  }

  /** POST /applications/{id}/withdraw. */
  withdraw(id: string): Observable<Application> {
    return this.http.post<Application>(`${this.baseUrl}/${id}/withdraw`, null);
  }

  /** POST /applications/{id}/advance — move an application to a target status. */
  advance(id: string, body: AdvanceApplicationStatusRequest): Observable<Application> {
    return this.http.post<Application>(`${this.baseUrl}/${id}/advance`, body);
  }
}
