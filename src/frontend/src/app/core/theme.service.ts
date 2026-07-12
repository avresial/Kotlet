import { effect, inject, Injectable } from '@angular/core';
import { AuthService } from './auth/auth.service';

export type Theme = 'light' | 'dark' | 'auto';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly auth = inject(AuthService);
  private readonly systemDark = matchMedia('(prefers-color-scheme: dark)');

  constructor() {
    effect(() => this.apply(this.auth.currentUser()?.theme ?? 'auto'));
    this.systemDark.addEventListener('change', () => this.apply(this.auth.currentUser()?.theme ?? 'auto'));
  }

  apply(theme: Theme): void {
    document.documentElement.classList.toggle('app-dark', theme === 'dark' || theme === 'auto' && this.systemDark.matches);
    document.documentElement.style.colorScheme = theme === 'auto' ? 'light dark' : theme;
  }
}
