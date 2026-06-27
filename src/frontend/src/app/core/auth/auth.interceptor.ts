import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { apiUrl } from '../http/api-url';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken();
  const isAuthEndpoint = request.url.startsWith(apiUrl('/api/auth/'));
  const authenticated = token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(authenticated).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthEndpoint) {
        return throwError(() => error);
      }
      return auth.refresh().pipe(
        switchMap((response) => {
          auth.setSession(response);
          return next(request.clone({ setHeaders: { Authorization: `Bearer ${response.accessToken}` } }));
        }),
      );
    }),
  );
};
