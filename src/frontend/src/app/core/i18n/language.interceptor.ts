import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TranslationService } from './translation.service';
import { apiUrl } from '../http/api-url';

/**
 * Tells the backend which language the user is currently viewing the app in, so that
 * translatable data (e.g. ingredient names) can be resolved server-side. Only API requests
 * are tagged; static asset requests (such as the i18n JSON files) are left untouched.
 */
export const languageInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith(apiUrl('/api/'))) {
    return next(request);
  }
  const translations = inject(TranslationService);
  return next(request.clone({ setHeaders: { 'Accept-Language': translations.language() } }));
};
