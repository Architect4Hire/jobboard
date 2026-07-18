import { TestBed } from '@angular/core/testing';

import { ApplicationStatus } from '../../core/models/application.model';
import { ApplicationStatusBadge } from './application-status';

describe('ApplicationStatusBadge', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [ApplicationStatusBadge] }));

  function render(status: ApplicationStatus): HTMLElement {
    const fixture = TestBed.createComponent(ApplicationStatusBadge);
    fixture.componentRef.setInput('status', status);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the label for the given status', () => {
    const badge = render(ApplicationStatus.Offered).querySelector('.badge');
    expect(badge?.textContent?.trim()).toBe('Offered');
  });

  it('tones a rejected application as danger', () => {
    const badge = render(ApplicationStatus.Rejected).querySelector('.badge');
    expect(badge?.getAttribute('data-tone')).toBe('danger');
  });
});
