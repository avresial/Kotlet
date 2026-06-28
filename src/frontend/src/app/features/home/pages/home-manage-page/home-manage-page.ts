import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { HomeService } from '../../home.service';
import { HomeDetail, HouseMember } from '../../home.models';

@Component({
  selector: 'app-home-manage-page',
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './home-manage-page.html',
  styleUrl: './home-manage-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeManagePage implements OnInit {
  private readonly homeService = inject(HomeService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly translations = inject(TranslationService);

  readonly home = signal<HomeDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly renaming = signal(false);
  readonly nameDraft = signal('');
  readonly savingName = signal(false);

  readonly inviteEmail = signal('');
  readonly inviting = signal(false);
  readonly inviteError = signal<string | null>(null);

  readonly busyMemberId = signal<string | null>(null);
  readonly busyInvitationId = signal<string | null>(null);
  readonly deleting = signal(false);

  readonly activeHouseId = computed(() => this.auth.currentUser()?.activeHouseId ?? null);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    const id = this.activeHouseId();
    if (!id) {
      this.loading.set(false);
      this.error.set(this.translations.translate('home.manage.noHome'));
      return;
    }
    this.loading.set(true);
    this.homeService.getHome(id).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (home) => this.home.set(home),
      error: (error) => this.error.set(getApiError(error, this.translations.translate('home.manage.loadError'))),
    });
  }

  startRename(): void {
    const home = this.home();
    if (!home) return;
    this.nameDraft.set(home.name);
    this.renaming.set(true);
  }

  cancelRename(): void {
    this.renaming.set(false);
  }

  saveName(): void {
    const home = this.home();
    const name = this.nameDraft().trim();
    if (!home || !name || this.savingName()) return;
    this.savingName.set(true);
    this.homeService.rename(home.id, name).pipe(finalize(() => this.savingName.set(false))).subscribe({
      next: () => {
        this.home.update((current) => (current ? { ...current, name } : current));
        this.renaming.set(false);
      },
      error: (error) => this.error.set(getApiError(error, this.translations.translate('home.manage.renameError'))),
    });
  }

  invite(): void {
    const home = this.home();
    const email = this.inviteEmail().trim();
    if (!home || !email || this.inviting()) return;
    this.inviting.set(true);
    this.inviteError.set(null);
    this.homeService.invite(home.id, email).pipe(finalize(() => this.inviting.set(false))).subscribe({
      next: (invitation) => {
        this.home.update((current) =>
          current ? { ...current, pendingInvitations: [...current.pendingInvitations, invitation] } : current);
        this.inviteEmail.set('');
      },
      error: (error) => this.inviteError.set(getApiError(error, this.translations.translate('home.invite.error'))),
    });
  }

  removeMember(member: HouseMember): void {
    const home = this.home();
    if (!home || this.busyMemberId()) return;
    this.busyMemberId.set(member.id);
    this.homeService.removeMember(home.id, member.id).pipe(finalize(() => this.busyMemberId.set(null))).subscribe({
      next: () => {
        if (member.isCurrentUser) {
          // We just left this home; bounce back through the dashboard which re-resolves the active home.
          void this.router.navigateByUrl('/dashboard');
          return;
        }
        this.home.update((current) =>
          current ? { ...current, members: current.members.filter((m) => m.id !== member.id) } : current);
      },
      error: (error) => this.error.set(getApiError(error, this.translations.translate('home.manage.removeError'))),
    });
  }

  cancelInvitation(invitationId: string): void {
    const home = this.home();
    if (!home || this.busyInvitationId()) return;
    this.busyInvitationId.set(invitationId);
    this.homeService.cancelInvitation(home.id, invitationId).pipe(finalize(() => this.busyInvitationId.set(null))).subscribe({
      next: () => this.home.update((current) =>
        current ? { ...current, pendingInvitations: current.pendingInvitations.filter((i) => i.id !== invitationId) } : current),
      error: (error) => this.error.set(getApiError(error, this.translations.translate('home.manage.cancelError'))),
    });
  }

  deleteHome(): void {
    const home = this.home();
    if (!home || this.deleting()) return;
    if (!confirm(this.translations.translate('home.manage.deleteConfirm'))) return;
    this.deleting.set(true);
    this.homeService.delete(home.id).pipe(finalize(() => this.deleting.set(false))).subscribe({
      next: () => void this.router.navigateByUrl('/dashboard'),
      error: (error) => this.error.set(getApiError(error, this.translations.translate('home.manage.deleteError'))),
    });
  }

  memberName(member: HouseMember): string {
    return member.displayName?.trim() || member.email.split('@')[0];
  }
}
