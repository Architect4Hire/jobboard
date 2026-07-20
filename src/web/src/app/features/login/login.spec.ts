import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AuthService } from '../../core/api/auth.service';
import { AuthToken } from '../../core/models/identity.model';
import { Login } from './login';

const TOKEN: AuthToken = {
  accessToken: 'jwt',
  tokenType: 'Bearer',
  expiresAtUtc: '2099-01-01T00:00:00Z',
};

describe('Login', () => {
  let login: ReturnType<typeof vi.fn>;
  let navigate: ReturnType<typeof vi.spyOn>;

  function setup(loginImpl = vi.fn().mockReturnValue(of(TOKEN))) {
    login = loginImpl;
    TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideRouter([]), { provide: AuthService, useValue: { login } }],
    });
    const fixture = TestBed.createComponent(Login);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
    return fixture;
  }

  it('logs in with the entered credentials and routes home', () => {
    const fixture = setup();
    fixture.componentInstance['form'].setValue({ email: 'a@b.io', password: 'secret' });
    fixture.componentInstance['submit']();

    expect(login).toHaveBeenCalledWith({ email: 'a@b.io', password: 'secret' });
    expect(navigate).toHaveBeenCalledWith(['/']);
  });

  it('surfaces an error and stays put on a failed login', () => {
    const fixture = setup(vi.fn().mockReturnValue(throwError(() => new Error('401'))));
    fixture.componentInstance['form'].setValue({ email: 'a@b.io', password: 'wrong' });
    fixture.componentInstance['submit']();
    fixture.detectChanges();

    expect(navigate).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).querySelector('.auth__error')).not.toBeNull();
  });
});
