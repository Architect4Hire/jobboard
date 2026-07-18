import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { API_BASE_URL } from '../api/api-base-url';
import { AuthToken } from '../models/identity.model';
import { authInterceptor } from './auth.interceptor';
import { TokenStore } from './token-store';

const TEST_TOKEN: AuthToken = {
  accessToken: 'jwt-123',
  tokenType: 'Bearer',
  expiresAtUtc: '2099-01-01T00:00:00Z',
};

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let tokenStore: TokenStore;
  let navigate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    navigate = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate } },
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    tokenStore = TestBed.inject(TokenStore);
    tokenStore.clear();
  });

  afterEach(() => {
    httpMock.verify();
    tokenStore.clear();
  });

  it('attaches the bearer token when one is stored', () => {
    tokenStore.set(TEST_TOKEN);

    http.get('/api/jobs').subscribe();

    const req = httpMock.expectOne('/api/jobs');
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-123');
    req.flush([]);
  });

  it('sends no Authorization header when signed out', () => {
    http.get('/api/jobs').subscribe();

    const req = httpMock.expectOne('/api/jobs');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('clears the token and routes to login on a 401 for an authorized request', () => {
    tokenStore.set(TEST_TOKEN);

    http.get('/api/applications').subscribe({ error: () => undefined });

    httpMock
      .expectOne('/api/applications')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(tokenStore.accessToken).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('does not attach the token to a non-gateway URL', () => {
    tokenStore.set(TEST_TOKEN);

    http.get('https://example.com/thing').subscribe();

    const req = httpMock.expectOne('https://example.com/thing');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('does not clear the token or redirect on a 401 for an unauthorized request (e.g. failed login)', () => {
    // No token stored: a login POST carries no Authorization header, so a 401 is a credentials failure,
    // not a stale-session signal — the interceptor must leave state alone and let the caller surface it.
    http.post('/api/identity/login', {}).subscribe({ error: () => undefined });

    httpMock
      .expectOne('/api/identity/login')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).not.toHaveBeenCalled();
  });
});
