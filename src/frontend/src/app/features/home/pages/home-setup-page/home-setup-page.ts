import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { HomeService } from '../../home.service';
import { IncomingInvitation } from '../../home.models';

@Component({
  selector: 'app-home-setup-page',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './home-setup-page.html',
  styleUrl: './home-setup-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeSetupPage implements OnInit {
  private readonly homeService = inject(HomeService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly translations = inject(TranslationService);

  readonly name = signal('');
  readonly creating = signal(false);
  readonly error = signal<string | null>(null);
  readonly invitations = signal<IncomingInvitation[]>([]);
  readonly invitationsLoading = signal(true);
  readonly busyInvitationId = signal<string | null>(null);

  ngOnInit(): void {
    // A returning user who already has a home should never see the setup screen.
    if (this.auth.currentUser()?.hasHome) {
      void this.router.navigateByUrl('/dashboard');
      return;
    }
    this.homeService.listMyInvitations().pipe(finalize(() => this.invitationsLoading.set(false))).subscribe({
      next: (invitations) => this.invitations.set(invitations),
      error: () => this.invitations.set([]),
    });
  }

  create(): void {
    const name = this.name().trim();
    if (!name || this.creating()) return;
    this.creating.set(true);
    this.error.set(null);
    this.homeService.create(name).pipe(finalize(() => this.creating.set(false))).subscribe({
      next: () => void this.router.navigateByUrl('/dashboard'),
      error: (error) => this.error.set(getApiError(error, this.translations.translate('home.setup.createError'))),
    });
  }

  accept(invitation: IncomingInvitation): void {
    if (this.busyInvitationId()) return;
    this.busyInvitationId.set(invitation.id);
    this.error.set(null);
    this.homeService.accept(invitation.id).pipe(finalize(() => this.busyInvitationId.set(null))).subscribe({
      next: () => void this.router.navigateByUrl('/dashboard'),
      error: (error) => this.error.set(getApiError(error, this.translations.translate('home.invite.acceptError'))),
    });
  }

  decline(invitation: IncomingInvitation): void {
    if (this.busyInvitationId()) return;
    this.busyInvitationId.set(invitation.id);
    this.homeService.decline(invitation.id).pipe(finalize(() => this.busyInvitationId.set(null))).subscribe({
      next: () => this.invitations.update((list) => list.filter((item) => item.id !== invitation.id)),
      error: (error) => this.error.set(getApiError(error, this.translations.translate('home.invite.declineError'))),
    });
  }
}
