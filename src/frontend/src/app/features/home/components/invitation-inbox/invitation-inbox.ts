import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, inject, input, output, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { IncomingInvitation } from '../../home.models';
import { HomeService } from '../../home.service';

@Component({
  selector: 'app-invitation-inbox',
  imports: [DatePipe, TranslatePipe],
  templateUrl: './invitation-inbox.html',
  styleUrl: './invitation-inbox.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvitationInbox {
  private readonly homeService = inject(HomeService);
  private readonly translations = inject(TranslationService);

  readonly invitations = input.required<readonly IncomingInvitation[]>();
  readonly joined = output<string>();
  readonly rejected = output<string>();
  readonly closed = output<void>();

  readonly busyInvitationId = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  join(invitation: IncomingInvitation): void {
    if (this.busyInvitationId()) return;
    this.busyInvitationId.set(invitation.id);
    this.error.set(null);
    this.homeService.accept(invitation.id).pipe(finalize(() => this.busyInvitationId.set(null))).subscribe({
      next: () => this.joined.emit(invitation.id),
      error: (error) => this.error.set(getApiError(error, this.translations.translate('home.invite.acceptError'))),
    });
  }

  reject(invitation: IncomingInvitation): void {
    if (this.busyInvitationId()) return;
    this.busyInvitationId.set(invitation.id);
    this.error.set(null);
    this.homeService.decline(invitation.id).pipe(finalize(() => this.busyInvitationId.set(null))).subscribe({
      next: () => this.rejected.emit(invitation.id),
      error: (error) => this.error.set(getApiError(error, this.translations.translate('home.invite.declineError'))),
    });
  }

  close(): void {
    if (!this.busyInvitationId()) this.closed.emit();
  }

  @HostListener('document:keydown.escape')
  closeFromEscape(): void {
    this.close();
  }
}
