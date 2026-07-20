import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';

import { authGuard } from './auth.guard';
import { Session } from './session';

describe('authGuard', () => {
  function run(isAuthenticated: boolean): boolean | UrlTree {
    const loginTree = new UrlTree();
    TestBed.configureTestingModule({
      providers: [
        { provide: Session, useValue: { isAuthenticated: () => isAuthenticated } },
        { provide: Router, useValue: { createUrlTree: () => loginTree } },
      ],
    });
    return TestBed.runInInjectionContext(() => authGuard({} as never, {} as never)) as
      | boolean
      | UrlTree;
  }

  it('allows a signed-in user through', () => {
    expect(run(true)).toBe(true);
  });

  it('redirects a signed-out user to a login UrlTree', () => {
    expect(run(false)).toBeInstanceOf(UrlTree);
  });
});
