import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { ProfileService } from '../../core/api/profile.service';
import { Session } from '../../core/auth/session';
import {
  CANDIDATE_AVAILABILITY_LABELS,
  CandidateAvailability,
  CandidateProfile as CandidateProfileModel,
  UpsertCandidateProfileRequest,
} from '../../core/models/profile.model';

/**
 * Candidate self-service profile: fill out metadata (contact, links, experience, availability, skills) and
 * upload a résumé file. Loads the current profile on init (a 404 just means "not created yet") and saves
 * through ProfileService (gateway `/profiles/candidates/{id}`); the candidate id comes from the Session
 * (the JWT), never a field. The résumé is managed separately from the JSON save via its own endpoints.
 */
@Component({
  selector: 'app-candidate-profile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  templateUrl: './candidate-profile.html',
  styleUrl: './candidate-profile.css',
})
export class CandidateProfile implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly profileService = inject(ProfileService);
  private readonly session = inject(Session);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal<string | null>(null);

  /** The persisted profile, kept for the résumé section (download path + filename). */
  protected readonly profile = signal<CandidateProfileModel | null>(null);
  protected readonly uploadingResume = signal(false);
  protected readonly resumeError = signal<string | null>(null);

  protected readonly candidateId = computed(() => this.session.userId());
  protected readonly hasResume = computed(() => this.profile()?.resumeUrl != null);

  /** Options for the availability <select>, built from the enum + its labels. */
  protected readonly availabilityOptions = (
    Object.values(CandidateAvailability).filter(
      (v) => typeof v === 'number',
    ) as CandidateAvailability[]
  ).map((value) => ({ value, label: CANDIDATE_AVAILABILITY_LABELS[value] }));

  protected readonly form = this.fb.group({
    headline: ['', [Validators.required, Validators.maxLength(200)]],
    summary: ['', [Validators.required, Validators.maxLength(4000)]],
    skills: [''],
    fullName: ['', Validators.maxLength(200)],
    location: ['', Validators.maxLength(200)],
    phone: ['', Validators.maxLength(50)],
    linkedInUrl: ['', Validators.maxLength(2048)],
    gitHubUrl: ['', Validators.maxLength(2048)],
    portfolioUrl: ['', Validators.maxLength(2048)],
    yearsOfExperience: [null as number | null, [Validators.min(0), Validators.max(70)]],
    desiredRole: ['', Validators.maxLength(200)],
    availability: [null as CandidateAvailability | null],
  });

  ngOnInit(): void {
    const id = this.candidateId();
    if (!id) {
      this.loading.set(false);
      return;
    }

    this.profileService
      .getCandidate(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (profile) => {
          this.profile.set(profile);
          this.patchForm(profile);
          this.loading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          // 404 = no profile yet; anything else is a real load failure.
          if (err.status !== 404) {
            this.error.set('Could not load your profile. Please try again.');
          }
          this.loading.set(false);
        },
      });
  }

  protected save(): void {
    const id = this.candidateId();
    if (this.form.invalid || !id || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.saved.set(false);
    this.error.set(null);

    this.profileService
      .upsertCandidate(id, this.buildRequest())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (profile) => {
          this.profile.set(profile);
          this.saving.set(false);
          this.saved.set(true);
        },
        error: () => {
          this.error.set('Could not save your profile. Please check the details and try again.');
          this.saving.set(false);
        },
      });
  }

  protected onResumeSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const id = this.candidateId();
    if (!file || !id) {
      return;
    }

    this.uploadingResume.set(true);
    this.resumeError.set(null);

    this.profileService
      .uploadCandidateResume(id, file)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (profile) => {
          this.profile.set(profile);
          this.uploadingResume.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.resumeError.set(
            err.status === 400
              ? 'That file is not a supported résumé (PDF or Word, up to 5 MB).'
              : 'Could not upload the résumé. Save your profile first, then try again.',
          );
          this.uploadingResume.set(false);
        },
      });

    // Reset the input so re-selecting the same file re-triggers change.
    input.value = '';
  }

  protected downloadResume(): void {
    const id = this.candidateId();
    const fileName = this.profile()?.resumeFileName;
    if (!id || !fileName) {
      return;
    }

    this.profileService
      .downloadCandidateResume(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (blob) => this.triggerDownload(blob, fileName),
        error: () => this.resumeError.set('Could not download the résumé.'),
      });
  }

  protected removeResume(): void {
    const id = this.candidateId();
    if (!id || this.uploadingResume()) {
      return;
    }

    this.uploadingResume.set(true);
    this.resumeError.set(null);

    this.profileService
      .deleteCandidateResume(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (profile) => {
          this.profile.set(profile);
          this.uploadingResume.set(false);
        },
        error: () => {
          this.resumeError.set('Could not remove the résumé. Please try again.');
          this.uploadingResume.set(false);
        },
      });
  }

  private patchForm(profile: CandidateProfileModel): void {
    this.form.patchValue({
      headline: profile.headline,
      summary: profile.summary,
      skills: profile.skills.join(', '),
      fullName: profile.fullName ?? '',
      location: profile.location ?? '',
      phone: profile.phone ?? '',
      linkedInUrl: profile.linkedInUrl ?? '',
      gitHubUrl: profile.gitHubUrl ?? '',
      portfolioUrl: profile.portfolioUrl ?? '',
      yearsOfExperience: profile.yearsOfExperience,
      desiredRole: profile.desiredRole ?? '',
      availability: profile.availability,
    });
  }

  private buildRequest(): UpsertCandidateProfileRequest {
    const value = this.form.getRawValue();
    return {
      headline: (value.headline ?? '').trim(),
      summary: (value.summary ?? '').trim(),
      skills: (value.skills ?? '')
        .split(',')
        .map((s) => s.trim())
        .filter((s) => s.length > 0),
      fullName: blankToNull(value.fullName),
      location: blankToNull(value.location),
      phone: blankToNull(value.phone),
      linkedInUrl: blankToNull(value.linkedInUrl),
      gitHubUrl: blankToNull(value.gitHubUrl),
      portfolioUrl: blankToNull(value.portfolioUrl),
      yearsOfExperience: value.yearsOfExperience,
      desiredRole: blankToNull(value.desiredRole),
      availability: value.availability,
    };
  }

  private triggerDownload(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}

/** Normalize an empty/whitespace field to null so the service sees absent and blank as one state. */
function blankToNull(value: string | null): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}
