import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import {
  CandidateProfile,
  EmployerProfile,
  UpsertCandidateProfileRequest,
  UpsertEmployerProfileRequest,
} from '../models/profile.model';
import { API_BASE_URL } from './api-base-url';

/**
 * Typed access to the Profiles endpoints behind the gateway (`/profiles/{candidates,employers}`). Employer
 * GET is public; the rest are protected — the auth interceptor supplies the JWT.
 */
@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/profiles`;

  /** GET /profiles/candidates/{candidateId}. */
  getCandidate(candidateId: string): Observable<CandidateProfile> {
    return this.http.get<CandidateProfile>(`${this.baseUrl}/candidates/${candidateId}`);
  }

  /** PUT /profiles/candidates/{candidateId} — create or replace a candidate profile. */
  upsertCandidate(
    candidateId: string,
    body: UpsertCandidateProfileRequest,
  ): Observable<CandidateProfile> {
    return this.http.put<CandidateProfile>(`${this.baseUrl}/candidates/${candidateId}`, body);
  }

  /** POST /profiles/candidates/{candidateId}/resume — upload (or replace) the résumé file. */
  uploadCandidateResume(candidateId: string, file: File): Observable<CandidateProfile> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<CandidateProfile>(
      `${this.baseUrl}/candidates/${candidateId}/resume`,
      form,
    );
  }

  /** DELETE /profiles/candidates/{candidateId}/resume — remove the résumé file. */
  deleteCandidateResume(candidateId: string): Observable<CandidateProfile> {
    return this.http.delete<CandidateProfile>(`${this.baseUrl}/candidates/${candidateId}/resume`);
  }

  /**
   * GET /profiles/candidates/{candidateId}/resume — the résumé bytes as a Blob. Fetched through HttpClient
   * (not a plain anchor) so the auth interceptor attaches the JWT the protected route requires; the caller
   * turns the Blob into an object URL to open or save.
   */
  downloadCandidateResume(candidateId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/candidates/${candidateId}/resume`, {
      responseType: 'blob',
    });
  }

  /** GET /profiles/employers/{employerId}. */
  getEmployer(employerId: string): Observable<EmployerProfile> {
    return this.http.get<EmployerProfile>(`${this.baseUrl}/employers/${employerId}`);
  }

  /** PUT /profiles/employers/{employerId} — create or replace an employer profile. */
  upsertEmployer(
    employerId: string,
    body: UpsertEmployerProfileRequest,
  ): Observable<EmployerProfile> {
    return this.http.put<EmployerProfile>(`${this.baseUrl}/employers/${employerId}`, body);
  }
}
