import { Injectable, signal } from '@angular/core';

import { AuthToken } from '../models/identity.model';

const STORAGE_KEY = 'jobboard.auth';

/**
 * Single source of the Identity JWT. Persists the token in localStorage so a reload stays signed in, and
 * is the only place the token lives — the auth interceptor reads it, AuthService sets/clears it, Session
 * derives the current principal from it. Held in a signal so nav/guards react to sign in/out without
 * subscriptions. Guards every localStorage touch so the app doesn't throw where storage is unavailable
 * (SSR, private modes).
 */
@Injectable({ providedIn: 'root' })
export class TokenStore {
  private readonly _token = signal<AuthToken | null>(this.read());

  /** The current bearer token as a signal, or null when signed out. */
  readonly token = this._token.asReadonly();

  /** The raw JWT string to attach as `Authorization: Bearer …`, or null when signed out. */
  get accessToken(): string | null {
    return this._token()?.accessToken ?? null;
  }

  set(token: AuthToken): void {
    this._token.set(token);
    this.write(token);
  }

  clear(): void {
    this._token.set(null);
    this.write(null);
  }

  private read(): AuthToken | null {
    const raw = this.storage?.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }
    try {
      return JSON.parse(raw) as AuthToken;
    } catch {
      return null;
    }
  }

  private write(token: AuthToken | null): void {
    if (!this.storage) {
      return;
    }
    if (token) {
      this.storage.setItem(STORAGE_KEY, JSON.stringify(token));
    } else {
      this.storage.removeItem(STORAGE_KEY);
    }
  }

  private get storage(): Storage | null {
    try {
      return typeof localStorage !== 'undefined' ? localStorage : null;
    } catch {
      return null;
    }
  }
}
