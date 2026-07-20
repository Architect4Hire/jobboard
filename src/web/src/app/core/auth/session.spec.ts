import { TestBed } from '@angular/core/testing';

import { AccountRole, AuthToken } from '../models/identity.model';
import { Session } from './session';
import { TokenStore } from './token-store';

/** Build a JWT-shaped string whose payload segment carries the given claims (signature is irrelevant here). */
function jwtWith(claims: Record<string, unknown>): string {
  const payload = btoa(JSON.stringify(claims)).replace(/\+/g, '-').replace(/\//g, '_');
  return `header.${payload}.signature`;
}

function tokenFor(claims: Record<string, unknown>): AuthToken {
  return { accessToken: jwtWith(claims), tokenType: 'Bearer', expiresAtUtc: '2099-01-01T00:00:00Z' };
}

describe('Session', () => {
  let session: Session;
  let tokenStore: TokenStore;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    session = TestBed.inject(Session);
    tokenStore = TestBed.inject(TokenStore);
    tokenStore.clear();
  });

  afterEach(() => tokenStore.clear());

  it('is signed out with no token', () => {
    expect(session.isAuthenticated()).toBe(false);
    expect(session.userId()).toBeNull();
    expect(session.role()).toBeNull();
  });

  it('decodes the account id and role from the JWT sub/role claims', () => {
    tokenStore.set(tokenFor({ sub: 'acct-1', email: 'e@x.io', role: 'Employer' }));

    expect(session.isAuthenticated()).toBe(true);
    expect(session.userId()).toBe('acct-1');
    expect(session.role()).toBe(AccountRole.Employer);
    expect(session.isEmployer()).toBe(true);
    expect(session.isCandidate()).toBe(false);
  });

  it('reacts to sign out', () => {
    tokenStore.set(tokenFor({ sub: 'acct-2', role: 'Candidate' }));
    expect(session.isCandidate()).toBe(true);

    tokenStore.clear();
    expect(session.isAuthenticated()).toBe(false);
    expect(session.userId()).toBeNull();
  });

  it('treats a malformed token as signed out', () => {
    tokenStore.set({ accessToken: 'not-a-jwt', tokenType: 'Bearer', expiresAtUtc: '2099-01-01T00:00:00Z' });
    expect(session.isAuthenticated()).toBe(false);
  });
});
