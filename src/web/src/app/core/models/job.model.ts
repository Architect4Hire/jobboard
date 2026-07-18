// Typed mirror of JobBoard.Jobs.Core ServiceModels / ViewModels. Field names are camelCase because the
// service serializes with ASP.NET Core's default JSON (no naming override); enums cross the wire as
// their numeric values (no JsonStringEnumConverter is registered), so JobStatus matches JobStatus.cs.

/** Mirrors Jobs' JobStatus domain enum — numeric values are the wire contract. */
export enum JobStatus {
  Draft = 0,
  Open = 1,
  Closed = 2,
}

/** Mirrors SalaryBandServiceModel. */
export interface SalaryBand {
  min: number;
  max: number;
  currency: string;
}

/** Mirrors JobClassificationServiceModel (a category or tag). */
export interface JobClassification {
  name: string;
  slug: string;
}

/** Mirrors JobDetailServiceModel — the full job returned by GET /jobs/{id}, POST /jobs, POST /jobs/{id}/close. */
export interface Job {
  id: string;
  title: string;
  description: string;
  location: string;
  salary: SalaryBand;
  status: JobStatus;
  employerId: string;
  categories: readonly JobClassification[];
  tags: readonly JobClassification[];
  createdOnUtc: string;
}

/** Mirrors JobSummaryServiceModel — the lighter list row returned by GET /jobs. */
export interface JobSummary {
  id: string;
  title: string;
  location: string;
  salary: SalaryBand;
  status: JobStatus;
  categorySlugs: readonly string[];
  createdOnUtc: string;
}

/** Mirrors SalaryBandViewModel. */
export interface SalaryBandInput {
  min: number;
  max: number;
  currency: string;
}

/** Mirrors JobClassificationViewModel. */
export interface JobClassificationInput {
  name: string;
  slug: string;
}

/** Mirrors PostJobViewModel — the POST /jobs request body. */
export interface PostJobRequest {
  title: string;
  description: string;
  location: string;
  salary: SalaryBandInput;
  employerId: string;
  categories: readonly JobClassificationInput[];
  tags: readonly JobClassificationInput[];
}
