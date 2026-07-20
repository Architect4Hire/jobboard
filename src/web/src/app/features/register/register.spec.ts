import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';

import { AuthService } from '../../core/api/auth.service';
import { AccountRole, Account, AuthToken } from '../../core/models/identity.model';
import { Register } from './register';

const ACCOUNT: Account = { id: 'acct-1', email: 'a@b.io', role: AccountRole.Candidate };
const TOKEN: AuthToken = {
  accessToken: 'jwt',
  tokenType: 'Bearer',
  expiresAtUtc: '2099-01-01T00:00:00Z',
};

describe('Register', () => {
  let register: ReturnType<typeof vi.fn>;
  let login: ReturnType<typeof vi.fn>;
  let navigate: ReturnType<typeof vi.spyOn>;

  function setup() {
    register = vi.fn().mockReturnValue(of(ACCOUNT));
    login = vi.fn().mockReturnValue(of(TOKEN));
    TestBed.configureTestingModule({
      imports: [Register],
      providers: [provideRouter([]), { provide: AuthService, useValue: { register, login } }],
    });
    const fixture = TestBed.createComponent(Register);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
    return fixture;
  }

  it('registers with the chosen role, then logs in and routes home', () => {
    const fixture = setup();
    fixture.componentInstance['form'].setValue({
      email: 'a@b.io',
      password: 'password1',
      role: AccountRole.Employer,
    });
    fixture.componentInstance['submit']();

    expect(register).toHaveBeenCalledWith({
      email: 'a@b.io',
      password: 'password1',
      role: AccountRole.Employer,
    });
    expect(login).toHaveBeenCalledWith({ email: 'a@b.io', password: 'password1' });
    expect(navigate).toHaveBeenCalledWith(['/']);
  });

  it('does not register an invalid form', () => {
    const fixture = setup();
    fixture.componentInstance['submit']();
    expect(register).not.toHaveBeenCalled();
  });
});
