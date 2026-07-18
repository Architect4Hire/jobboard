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
