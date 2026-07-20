// Typed mirror of JobBoard.Identity.Core ServiceModels / ViewModels. camelCase fields; AccountRole crosses
// the wire as its numeric value (no JsonStringEnumConverter), matching AccountRole.cs (declaration order).

/** Mirrors Identity's AccountRole domain enum — numeric values follow the C# declaration order. */
export enum AccountRole {
  Employer = 0,
  Candidate = 1,
}

/** Mirrors AccountServiceModel — the account returned by POST /identity/register. */
export interface Account {
  id: string;
  email: string;
  role: AccountRole;
}

/** Mirrors AuthTokenServiceModel — the bearer token returned by POST /identity/login. */
export interface AuthToken {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
}

/** Mirrors LoginViewModel — the POST /identity/login request body. */
export interface LoginRequest {
  email: string;
  password: string;
}

/** Mirrors RegisterAccountViewModel — the POST /identity/register request body. */
export interface RegisterAccountRequest {
  email: string;
  password: string;
  role: AccountRole;
}
