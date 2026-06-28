import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';

import { AuthService } from './core/auth/auth.service';
import { authInterceptor } from './core/auth/auth.interceptor';
import { routes } from './app.routes';
import { KotletPreset } from './theme/kotlet-preset';
import { TranslationService } from './core/i18n/translation.service';
import { languageInterceptor } from './core/i18n/language.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideAnimationsAsync(),
    providePrimeNG({ theme: { preset: KotletPreset, options: { darkModeSelector: false } }, ripple: true }),
    provideHttpClient(withInterceptors([authInterceptor, languageInterceptor])),
    provideRouter(routes),
    provideAppInitializer(() => inject(TranslationService).initialize()),
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ]
};
