import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { ApplicationStatus } from '../../core/models/application.model';

/** Presentational: renders an ApplicationStatus as a labelled, colour-toned badge. No data access. */
@Component({
  selector: 'app-application-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './application-status.html',
  styleUrl: './application-status.css',
})
export class ApplicationStatusBadge {
  readonly status = input.required<ApplicationStatus>();

  protected readonly display = computed(() => STATUS_DISPLAY[this.status()]);
}

/** Label + tone per status. Tone drives the badge colour via a `data-tone` attribute in the template. */
const STATUS_DISPLAY: Record<ApplicationStatus, { label: string; tone: string }> = {
  [ApplicationStatus.Submitted]: { label: 'Submitted', tone: 'info' },
  [ApplicationStatus.Reviewed]: { label: 'Reviewed', tone: 'info' },
  [ApplicationStatus.Offered]: { label: 'Offered', tone: 'success' },
  [ApplicationStatus.Rejected]: { label: 'Rejected', tone: 'danger' },
  [ApplicationStatus.Withdrawn]: { label: 'Withdrawn', tone: 'muted' },
};
