import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { getApiError } from '../../core/http/api-error';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './app-header.html',
  styleUrl: './app-header.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppHeader {
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);
  readonly isLoggingOut = signal(false);
  readonly logoutError = signal<string | null>(null);

  logout(): void {
    if (this.isLoggingOut()) {
      return;
    }

    this.isLoggingOut.set(true);
    this.logoutError.set(null);
    this.auth.logout().pipe(finalize(() => this.isLoggingOut.set(false))).subscribe({
      next: () => void this.router.navigateByUrl('/login'),
      error: (error) => this.logoutError.set(getApiError(error, 'Unable to log out. Please try again.')),
    });
  }
}
