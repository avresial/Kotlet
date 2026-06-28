import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

/**
 * Guards the main app: requires an authenticated user who belongs to at least one home.
 * Users without a home are sent to the first-login setup screen.
 */
export const homeGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.restoreSession();
  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }
  return auth.currentUser()?.hasHome ? true : router.createUrlTree(['/home/setup']);
};
