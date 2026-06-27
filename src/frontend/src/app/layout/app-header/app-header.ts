import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-header',
  imports: [RouterLink],
  templateUrl: './app-header.html',
  styleUrl: './app-header.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppHeader {
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);
  readonly isLoggingOut = signal(false);

  logout(): void {
    this.isLoggingOut.set(true);
    this.auth.logout().pipe(finalize(() => this.isLoggingOut.set(false))).subscribe({
      next: () => void this.router.navigateByUrl('/login'),
    });
  }
}
