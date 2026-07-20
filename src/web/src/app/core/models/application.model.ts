// Typed mirror of JobBoard.Applications.Core ServiceModels / ViewModels. camelCase fields; ApplicationStatus
// crosses the wire as its numeric value (no JsonStringEnumConverter), matching ApplicationStatus.cs.

/** Mirrors Applications' ApplicationStatus domain enum — numeric values are the wire contract. */
export enum ApplicationStatus {
  Submitted = 0,
  Reviewed = 1,
  Offered = 2,
  Rejected = 3,
  Withdrawn = 4,
}

/** Mirrors ApplicationDetailServiceModel — GET /applications/{id}, POST /applications, .../withdraw, .../advance. */
export interface Application {
  id: string;
  candidateId: string;
  jobId: string;
  status: ApplicationStatus;
  resumeReference: string | null;
  submittedOnUtc: string;
  statusChangedOnUtc: string;
}

/** Mirrors ApplicationSummaryServiceModel — the lighter list row returned by GET /applications?candidateId=. */
export interface ApplicationSummary {
  id: string;
  jobId: string;
  status: ApplicationStatus;
  submittedOnUtc: string;
  statusChangedOnUtc: string;
}

/** Mirrors SubmitApplicationViewModel — the POST /applications request body. */
export interface SubmitApplicationRequest {
  candidateId: string;
  jobId: string;
  resumeReference: string | null;
}

/** Mirrors AdvanceApplicationStatusViewModel — the POST /applications/{id}/advance request body. */
export interface AdvanceApplicationStatusRequest {
  targetStatus: ApplicationStatus;
}
