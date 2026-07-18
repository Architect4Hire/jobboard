// Typed mirror of JobBoard.Profiles.Core ServiceModels / ViewModels. camelCase fields to match the
// service's default ASP.NET Core JSON serialization.

/** Mirrors CandidateProfileServiceModel — GET/PUT /profiles/candidates/{candidateId}. */
export interface CandidateProfile {
  candidateId: string;
  headline: string;
  summary: string;
  skills: readonly string[];
  resumeUrl: string | null;
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

/** Mirrors UpsertCandidateProfileViewModel — the PUT /profiles/candidates/{id} request body. */
export interface UpsertCandidateProfileRequest {
  headline: string;
  summary: string;
  skills: readonly string[];
  resumeUrl: string | null;
}

/** Mirrors UpsertEmployerProfileViewModel — the PUT /profiles/employers/{id} request body. */
export interface UpsertEmployerProfileRequest {
  companyName: string;
  website: string | null;
  description: string;
}
