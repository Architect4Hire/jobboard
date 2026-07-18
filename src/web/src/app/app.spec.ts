import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';
import { API_BASE_URL } from './core/api/api-base-url';
import { TokenStore } from './core/auth/token-store';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    }).compileComponents();
    TestBed.inject(TokenStore).clear();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the brand and a log-in link when signed out', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.topbar__brand')?.textContent).toContain('JobBoard');
    expect(compiled.querySelector('.topbar__auth a')?.textContent).toContain('Log in');
  });
});
