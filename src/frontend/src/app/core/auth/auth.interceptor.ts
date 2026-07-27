import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { apiUrl } from '../http/api-url';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken();
  const isApiRequest = request.url.startsWith(apiUrl('/api/'));
  const isAuthEndpoint = request.url.startsWith(apiUrl('/api/auth/'));
  const authenticated = token && isApiRequest ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(authenticated).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !isApiRequest || isAuthEndpoint) {
        return throwError(() => error);
      }
      // refreshSession collapses parallel 401s into one call: the API rotates the refresh token
      // and revokes the whole family when a rotated one is replayed, so racing refreshes would
      // sign the user out everywhere.
      return auth.refreshSession().pipe(
        switchMap((response) =>
          next(request.clone({ setHeaders: { Authorization: `Bearer ${response.accessToken}` } })),
        ),
      );
    }),
  );
};
