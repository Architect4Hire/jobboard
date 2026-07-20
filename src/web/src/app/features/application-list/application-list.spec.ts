import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { ApplicationService } from '../../core/api/application.service';
import {
  Application,
  ApplicationHistoryItem,
  ApplicationStatus,
} from '../../core/models/application.model';
import { ApplicationList } from './application-list';

const HISTORY: readonly ApplicationHistoryItem[] = [
  {
    id: 'a1',
    jobId: 'j1',
    jobTitle: 'Senior Engineer',
    employerId: 'e1',
    employerName: 'Acme Co',
    status: ApplicationStatus.Submitted,
    submittedOnUtc: '2026-01-01T00:00:00Z',
    statusChangedOnUtc: '2026-01-01T00:00:00Z',
  },
];

const WITHDRAWN: Application = {
  id: 'a1',
  candidateId: 'cand-1',
  jobId: 'j1',
  status: ApplicationStatus.Withdrawn,
  resumeReference: null,
  submittedOnUtc: '2026-01-01T00:00:00Z',
  statusChangedOnUtc: '2026-01-05T00:00:00Z',
};

describe('ApplicationList', () => {
  let listMine: ReturnType<typeof vi.fn>;
  let withdraw: ReturnType<typeof vi.fn>;

  function setup() {
    listMine = vi.fn().mockReturnValue(of(HISTORY));
    withdraw = vi.fn().mockReturnValue(of(WITHDRAWN));
    TestBed.configureTestingModule({
      imports: [ApplicationList],
      providers: [provideRouter([]), { provide: ApplicationService, useValue: { listMine, withdraw } }],
    });
    const fixture = TestBed.createComponent(ApplicationList);
    fixture.detectChanges();
    return fixture;
  }

  it("lists the caller's own applications with job title, employer name, and a status badge", () => {
    const fixture = setup();
    expect(listMine).toHaveBeenCalled();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.row__job')?.textContent?.trim()).toBe('Senior Engineer');
    expect(element.querySelector('.row__employer')?.textContent?.trim()).toBe('Acme Co');
    expect(element.querySelector('.badge')?.textContent?.trim()).toBe('Submitted');
  });

  it('withdraws an application and updates its status in place', () => {
    const fixture = setup();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('.row__withdraw')!
      .click();
    fixture.detectChanges();

    expect(withdraw).toHaveBeenCalledWith('a1');
    const badge = (fixture.nativeElement as HTMLElement).querySelector('.badge');
    expect(badge?.textContent?.trim()).toBe('Withdrawn');
  });

  it('shows an error message when the list fetch fails', () => {
    listMine = vi.fn().mockReturnValue(throwError(() => new Error('down')));
    withdraw = vi.fn();
    TestBed.configureTestingModule({
      imports: [ApplicationList],
      providers: [provideRouter([]), { provide: ApplicationService, useValue: { listMine, withdraw } }],
    });
    const fixture = TestBed.createComponent(ApplicationList);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.load-error')).not.toBeNull();
  });
});
