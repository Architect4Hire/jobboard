// Typed mirror of JobBoard.Profiles.Core ServiceModels / ViewModels. camelCase fields to match the
// service's default ASP.NET Core JSON serialization; CandidateAvailability crosses the wire as its numeric
// value (no JsonStringEnumConverter), matching CandidateAvailability.cs (declaration order).

/** Mirrors Profiles' CandidateAvailability domain enum — numeric values follow the C# declaration order. */
export enum CandidateAvailability {
  Immediate = 0,
  WithinTwoWeeks = 1,
  WithinAMonth = 2,
  NotLooking = 3,
}

/** Human labels for the availability options, for select controls / display. */
export const CANDIDATE_AVAILABILITY_LABELS: Readonly<Record<CandidateAvailability, string>> = {
  [CandidateAvailability.Immediate]: 'Immediately',
  [CandidateAvailability.WithinTwoWeeks]: 'Within two weeks',
  [CandidateAvailability.WithinAMonth]: 'Within a month',
  [CandidateAvailability.NotLooking]: 'Not actively looking',
};

/** Mirrors CandidateProfileServiceModel — GET/PUT /profiles/candidates/{candidateId}. */
export interface CandidateProfile {
  candidateId: string;
  headline: string;
  summary: string;
  skills: readonly string[];
  fullName: string | null;
  location: string | null;
  phone: string | null;
  linkedInUrl: string | null;
  gitHubUrl: string | null;
  portfolioUrl: string | null;
  yearsOfExperience: number | null;
  desiredRole: string | null;
  availability: CandidateAvailability | null;
  /** Gateway-relative download path for the uploaded résumé, or null when none is on file. */
  resumeUrl: string | null;
  resumeFileName: string | null;
  updatedOnUtc: string;
}

/** Mirrors EmployerProfileServiceModel — GET/PUT /profiles/employers/{employerId}. */
export interface EmployerProfile {
  employerId: string;
  companyName: string;
  website: string | null;
  description: string;
  updatedOnUtc: string;
}

/** Mirrors UpsertCandidateProfileViewModel — the PUT /profiles/candidates/{id} request body. The résumé
 * is managed by the dedicated upload/download/delete endpoints, so it is not part of this body. */
export interface UpsertCandidateProfileRequest {
  headline: string;
  summary: string;
  skills: readonly string[];
  fullName: string | null;
  location: string | null;
  phone: string | null;
  linkedInUrl: string | null;
  gitHubUrl: string | null;
  portfolioUrl: string | null;
  yearsOfExperience: number | null;
  desiredRole: string | null;
  availability: CandidateAvailability | null;
}

/** Mirrors UpsertEmployerProfileViewModel — the PUT /profiles/employers/{id} request body. */
export interface UpsertEmployerProfileRequest {
  companyName: string;
  website: string | null;
  description: string;
}
