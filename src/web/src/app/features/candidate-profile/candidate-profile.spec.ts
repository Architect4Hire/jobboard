import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ProfileService } from '../../core/api/profile.service';
import { Session } from '../../core/auth/session';
import {
  CandidateAvailability,
  CandidateProfile as CandidateProfileModel,
} from '../../core/models/profile.model';
import { CandidateProfile } from './candidate-profile';

const PROFILE: CandidateProfileModel = {
  candidateId: 'cand-1',
  headline: 'Engineer',
  summary: 'Builds things.',
  skills: ['c#', 'angular'],
  fullName: 'Sam Example',
  location: 'Remote',
  phone: null,
  linkedInUrl: 'https://linkedin.com/in/sam',
  gitHubUrl: null,
  portfolioUrl: null,
  yearsOfExperience: 8,
  desiredRole: 'Staff Engineer',
  availability: CandidateAvailability.Immediate,
  resumeUrl: null,
  resumeFileName: null,
  updatedOnUtc: '2026-01-01T00:00:00Z',
};

describe('CandidateProfile', () => {
  let getCandidate: ReturnType<typeof vi.fn>;
  let upsertCandidate: ReturnType<typeof vi.fn>;

  function setup() {
    getCandidate = vi.fn().mockReturnValue(of(PROFILE));
    upsertCandidate = vi.fn().mockReturnValue(of(PROFILE));
    TestBed.configureTestingModule({
      imports: [CandidateProfile],
      providers: [
        { provide: ProfileService, useValue: { getCandidate, upsertCandidate } },
        { provide: Session, useValue: { userId: (() => 'cand-1') as Session['userId'] } },
      ],
    });
    const fixture = TestBed.createComponent(CandidateProfile);
    fixture.detectChanges();
    return fixture;
  }

  it('loads the profile for the session candidate id on init', () => {
    setup();
    expect(getCandidate).toHaveBeenCalledWith('cand-1');
  });

  it('saves the parsed request (skills split, blanks nulled) with the session candidate id', () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    component['form'].patchValue({ skills: 'c#, azure,  ', phone: '   ' });

    component['save']();

    expect(upsertCandidate).toHaveBeenCalledWith(
      'cand-1',
      expect.objectContaining({
        headline: 'Engineer',
        skills: ['c#', 'azure'],
        phone: null,
        availability: CandidateAvailability.Immediate,
      }),
    );
  });

  it('does not save an invalid form', () => {
    const fixture = setup();
    fixture.componentInstance['form'].patchValue({ headline: '' });
    fixture.componentInstance['save']();
    expect(upsertCandidate).not.toHaveBeenCalled();
  });
});
