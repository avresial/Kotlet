import { effect, inject, Injectable, signal } from '@angular/core';
import { AuthService } from './auth/auth.service';

export type Theme = 'light' | 'dark' | 'auto';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly auth = inject(AuthService);
  private readonly systemDark = matchMedia('(prefers-color-scheme: dark)');
  private readonly forcedLight = signal(false);

  constructor() {
    effect(() => this.apply(this.resolveTheme()));
    this.systemDark.addEventListener('change', () => this.apply(this.resolveTheme()));
  }

  apply(theme: Theme): void {
    document.documentElement.classList.toggle('app-dark', theme === 'dark' || theme === 'auto' && this.systemDark.matches);
    document.documentElement.style.colorScheme = theme === 'auto' ? 'light dark' : theme;
  }

  /** Pin the app to the light palette regardless of the user/system preference. */
  forceLight(on: boolean): void {
    this.forcedLight.set(on);
  }

  private resolveTheme(): Theme {
    const preference = this.auth.currentUser()?.theme ?? 'auto';
    return this.forcedLight() ? 'light' : preference;
  }
}
