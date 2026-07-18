import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { TokenStore } from '../auth/token-store';
import {
  Account,
  AuthToken,
  LoginRequest,
  RegisterAccountRequest,
} from '../models/identity.model';
import { API_BASE_URL } from './api-base-url';

/**
 * Typed access to the Identity endpoints behind the gateway (`/identity`, public routes). Owns the token
 * lifecycle at the app level: login stores the JWT in TokenStore (whence the auth interceptor reads it),
 * logout clears it. Register just creates the account — the client logs in afterwards to get a token.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokenStore = inject(TokenStore);
  private readonly baseUrl = `${inject(API_BASE_URL)}/identity`;

  /** POST /identity/register — create a new account. */
  register(body: RegisterAccountRequest): Observable<Account> {
    return this.http.post<Account>(`${this.baseUrl}/register`, body);
  }

  /** POST /identity/login — exchange credentials for a JWT, and store it for the interceptor. */
  login(body: LoginRequest): Observable<AuthToken> {
    return this.http
      .post<AuthToken>(`${this.baseUrl}/login`, body)
      .pipe(tap((token) => this.tokenStore.set(token)));
  }

  /** Drop the stored token — sign out client-side. */
  logout(): void {
    this.tokenStore.clear();
  }
}
