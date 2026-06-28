import { ChangeDetectionStrategy, Component, HostListener, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { getApiError } from '../../core/http/api-error';
import { LanguageSwitcher } from '../../core/i18n/language-switcher/language-switcher';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import { HomeService } from '../../features/home/home.service';
import { HomeSummary } from '../../features/home/home.models';

@Component({
  selector: 'app-header',
  imports: [LanguageSwitcher, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './app-header.html',
  styleUrl: './app-header.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppHeader {
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);
  private readonly translations = inject(TranslationService);
  private readonly homeService = inject(HomeService);
  readonly isLoggingOut = signal(false);
  readonly logoutError = signal<string | null>(null);
  readonly isAccountMenuOpen = signal(false);
  readonly isDrawerOpen = signal(false);

  readonly homes = signal<HomeSummary[]>([]);
  readonly homesLoading = signal(false);
  readonly invitationCount = signal(0);
  readonly switchingHouseId = signal<string | null>(null);

  toggleAccountMenu(event: MouseEvent): void {
    event.stopPropagation();
    const willOpen = !this.isAccountMenuOpen();
    this.isAccountMenuOpen.set(willOpen);
    if (willOpen) {
      this.loadHomes();
    }
  }

  private loadHomes(): void {
    this.homesLoading.set(true);
    this.homeService.listHomes().pipe(finalize(() => this.homesLoading.set(false))).subscribe({
      next: (homes) => this.homes.set(homes),
      error: () => this.homes.set([]),
    });
    this.homeService.listMyInvitations().subscribe({
      next: (invitations) => this.invitationCount.set(invitations.length),
      error: () => this.invitationCount.set(0),
    });
  }

  switchHome(house: HomeSummary): void {
    if (house.isActive || this.switchingHouseId()) return;
    this.switchingHouseId.set(house.id);
    this.homeService.switch(house.id).pipe(finalize(() => this.switchingHouseId.set(null))).subscribe({
      next: () => {
        this.closeAccountMenu();
        this.reloadCurrentRoute();
      },
      error: (error) => this.logoutError.set(getApiError(error, this.translations.translate('home.switch.error'))),
    });
  }

  private reloadCurrentRoute(): void {
    const url = this.router.url;
    void this.router.navigateByUrl('/', { skipLocationChange: true }).then(() => this.router.navigateByUrl(url));
  }

  closeAccountMenu(): void {
    this.isAccountMenuOpen.set(false);
  }

  toggleDrawer(): void {
    this.isDrawerOpen.update((isOpen) => !isOpen);
    this.closeAccountMenu();
  }

  closeDrawer(): void {
    this.isDrawerOpen.set(false);
    this.closeAccountMenu();
  }

  @HostListener('document:click')
  closeAccountMenuFromDocument(): void {
    this.closeAccountMenu();
  }

  @HostListener('document:keydown.escape')
  closeMenusFromEscape(): void {
    this.closeDrawer();
  }

  logout(): void {
    if (this.isLoggingOut()) {
      return;
    }

    this.isLoggingOut.set(true);
    this.closeDrawer();
    this.logoutError.set(null);
    this.auth.logout().pipe(finalize(() => this.isLoggingOut.set(false))).subscribe({
      next: () => void this.router.navigateByUrl('/login'),
      error: (error) => this.logoutError.set(getApiError(error, this.translations.translate('auth.logout.error'))),
    });
  }
}
