import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { TokenStore } from '../auth/token-store';
import { AccountRole, AuthToken } from '../models/identity.model';
import { API_BASE_URL } from './api-base-url';
import { AuthService } from './auth.service';

const TOKEN: AuthToken = {
  accessToken: 'jwt-xyz',
  tokenType: 'Bearer',
  expiresAtUtc: '2099-01-01T00:00:00Z',
};

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let tokenStore: TokenStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    tokenStore = TestBed.inject(TokenStore);
    tokenStore.clear();
  });

  afterEach(() => {
    httpMock.verify();
    tokenStore.clear();
  });

  it('POST /api/identity/login stores the returned token', () => {
    service.login({ email: 'a@b.com', password: 'pw' }).subscribe();

    const req = httpMock.expectOne('/api/identity/login');
    expect(req.request.method).toBe('POST');
    req.flush(TOKEN);

    expect(tokenStore.accessToken).toBe('jwt-xyz');
  });

  it('POST /api/identity/register sends the role as a numeric enum value', () => {
    service
      .register({ email: 'a@b.com', password: 'pw', role: AccountRole.Candidate })
      .subscribe();

    const req = httpMock.expectOne('/api/identity/register');
    expect(req.request.body.role).toBe(1);
    req.flush({ id: 'x', email: 'a@b.com', role: AccountRole.Candidate });

    // Register does not authenticate — no token should be stored.
    expect(tokenStore.accessToken).toBeNull();
  });

  it('logout clears the stored token', () => {
    tokenStore.set(TOKEN);
    service.logout();
    expect(tokenStore.accessToken).toBeNull();
  });
});
