import { computed, inject, Injectable } from '@angular/core';

import { AccountRole } from '../models/identity.model';
import { TokenStore } from './token-store';

/** The signed-in principal, decoded from the Identity JWT (never fetched over HTTP). */
export interface SessionUser {
  /** The account id — the JWT `sub` claim. Used as employerId/candidateId by the services. */
  id: string;
  email: string | null;
  role: AccountRole | null;
}

/**
 * The current principal, derived reactively from the JWT in TokenStore. The account id (`sub` claim) is
 * what the services want as employerId/candidateId, so components read it from here rather than passing
 * ids around. This decodes an already-issued token client-side; it is not an authorization boundary (the
 * services validate the JWT) — it only drives what the UI offers.
 */
@Injectable({ providedIn: 'root' })
export class Session {
  private readonly tokenStore = inject(TokenStore);

  /** The decoded principal, or null when signed out. */
  readonly user = computed<SessionUser | null>(() => {
    const token = this.tokenStore.token();
    return token ? decodePrincipal(token.accessToken) : null;
  });

  readonly isAuthenticated = computed(() => this.user() !== null);

  /** The account id to send as employerId/candidateId, or null when signed out. */
  readonly userId = computed(() => this.user()?.id ?? null);

  readonly role = computed(() => this.user()?.role ?? null);

  readonly isEmployer = computed(() => this.role() === AccountRole.Employer);

  readonly isCandidate = computed(() => this.role() === AccountRole.Candidate);
}

/** Shape of the JWT claims Identity issues (JwtTokenIssuer): `sub`, `email`, `role` (role as its name). */
interface JwtClaims {
  sub?: string;
  email?: string;
  role?: string;
}

/** Decode a JWT's payload segment and map it to a SessionUser. Returns null for a malformed token. */
function decodePrincipal(accessToken: string): SessionUser | null {
  const claims = decodeClaims(accessToken);
  if (!claims?.sub) {
    return null;
  }
  return {
    id: claims.sub,
    email: claims.email ?? null,
    role: parseRole(claims.role),
  };
}

function decodeClaims(accessToken: string): JwtClaims | null {
  const payload = accessToken.split('.')[1];
  if (!payload) {
    return null;
  }
  try {
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(json) as JwtClaims;
  } catch {
    return null;
  }
}

/** Map the `role` claim (the AccountRole name Identity writes) back to the enum. */
function parseRole(role: string | undefined): AccountRole | null {
  switch (role) {
    case 'Employer':
      return AccountRole.Employer;
    case 'Candidate':
      return AccountRole.Candidate;
    default:
      return null;
  }
}
