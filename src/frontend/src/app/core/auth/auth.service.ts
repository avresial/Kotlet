import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { finalize, firstValueFrom, Observable, retry, shareReplay, tap, timer } from 'rxjs';
import {
  AuthResponse,
  ChangePasswordRequest,
  CurrentUser,
  LoginRequest,
  RegisterRequest,
  UpdateProfileRequest,
} from './auth.models';
import { apiUrl } from '../http/api-url';
import { TranslationService } from '../i18n/translation.service';

/**
 * Backoff between refresh attempts while the API is unreachable. The API runs on an Azure
 * plan that puts the site to sleep when idle, and a cold start answers the first requests
 * with a gateway error for tens of seconds; without the wait that window looks exactly like
 * an expired session, bouncing the user to the login page on reload and failing whatever
 * they clicked mid-session.
 */
const wakeUpDelaysMs = [500, 1_000, 2_000, 4_000, 8_000, 8_000];

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly translations = inject(TranslationService);
  private readonly currentUserState = signal<CurrentUser | null>(null);
  private readonly accessTokenState = signal<string | null>(null);
  private readonly apiUnreachableState = signal(false);
  private restoration: Promise<void> | null = null;
  private refreshInFlight: Observable<AuthResponse> | null = null;

  readonly currentUser = this.currentUserState.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserState() !== null);
  readonly accessToken = this.accessTokenState.asReadonly();
  /** True while the API is not answering, so the UI can say so instead of showing a bare login page. */
  readonly apiUnreachable = this.apiUnreachableState.asReadonly();

  /**
   * Attempts to restore the session from the refresh-token cookie. The work is
   * memoized so the app initializer and the route guards share a single refresh
   * call: guards await this promise before deciding, which prevents a redirect
   * to the login page while the refresh request is still in flight on reload.
   */
  restoreSession(): Promise<void> {
    return (this.restoration ??= this.restoreFromRefreshToken().then((outcome) => {
      // Only a definitive answer is memoized. Giving up because the API never woke up is not
      // one, so the next guard run gets to try again rather than inheriting a dead session.
      if (outcome === 'unreachable') this.restoration = null;
    }));
  }

  private async restoreFromRefreshToken(): Promise<'settled' | 'unreachable'> {
    try {
      await firstValueFrom(this.refreshSession());
      return 'settled';
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) return 'settled';
      console.error('Unable to restore the session.', error);
      return 'unreachable';
    }
  }

  /**
   * Refreshes the session, sharing one request between concurrent callers and waiting out a
   * sleeping API. The API rotates the refresh token on every call and treats a rotated token that
   * comes back as theft, revoking every token the user owns. Two requests that hit 401 at the same
   * time — routine once the access token has expired while the API was asleep — would send the
   * same cookie twice and trigger exactly that, so they have to queue behind a single call.
   *
   * Retrying is safe against that same rotation: if a refresh succeeded but its response was lost,
   * the retry presents the pre-rotation cookie and the API answers it from the live replacement.
   */
  refreshSession(): Observable<AuthResponse> {
    return (this.refreshInFlight ??= this.refresh().pipe(
      retry({
        delay: (error: unknown, attempt) => {
          // Only the API's own verdict ends the attempt. Everything else — a gateway error, a
          // timeout, no connection at all — is the site still waking up.
          if (error instanceof HttpErrorResponse && error.status === 401) throw error;
          if (attempt > wakeUpDelaysMs.length) throw error;
          this.apiUnreachableState.set(true);
          return timer(wakeUpDelaysMs[attempt - 1]);
        },
      }),
      tap((response) => {
        this.setSession(response);
        this.apiUnreachableState.set(false);
      }),
      tap({
        error: (error: unknown) => {
          // A 401 is the API's final word: the cookie is gone, expired or revoked. Drop the local
          // session so the app shows a login page instead of a signed-in shell that 401s on every
          // request. A transient failure that outlasted the backoff leaves the session alone —
          // mid-session the user can retry, and at startup there is nothing to clear.
          if (error instanceof HttpErrorResponse && error.status === 401) {
            this.clearSession();
            this.apiUnreachableState.set(false);
          }
        },
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    ));
  }

  login(request: LoginRequest) {
    return this.http
      .post<AuthResponse>(apiUrl('/api/auth/login'), request, { withCredentials: true })
      .pipe(tap((response) => this.setSession(response)));
  }

  register(request: RegisterRequest) {
    return this.http
      .post<AuthResponse>(apiUrl('/api/auth/register'), request, { withCredentials: true })
      .pipe(tap((response) => this.setSession(response)));
  }

  // Private on purpose: callers go through refreshSession so the rotation cannot be raced.
  private refresh() {
    return this.http.post<AuthResponse>(apiUrl('/api/auth/refresh'), null, { withCredentials: true });
  }

  updateProfile(request: UpdateProfileRequest) {
    return this.http
      .put<CurrentUser>(apiUrl('/api/auth/profile'), request, { withCredentials: true })
      .pipe(tap((user) => this.currentUserState.set(user)));
  }

  /** Replaces the access token in use, e.g. after switching the active home. */
  applyToken(accessToken: string): void {
    this.accessTokenState.set(accessToken);
  }

  /** Re-fetches the current user so derived fields (active home, hasHome) stay in sync. */
  reloadUser() {
    return this.http
      .get<CurrentUser>(apiUrl('/api/auth/me'), { withCredentials: true })
      .pipe(tap((user) => this.currentUserState.set(user)));
  }

  changePassword(request: ChangePasswordRequest) {
    return this.http.post<void>(apiUrl('/api/auth/password'), request, { withCredentials: true });
  }

  logout() {
    return this.http
      .post<void>(apiUrl('/api/auth/logout'), null, { withCredentials: true })
      .pipe(tap(() => this.clearSession()));
  }

  setSession(response: AuthResponse): void {
    this.currentUserState.set(response.user);
    this.accessTokenState.set(response.accessToken);
    if (response.user.preferredLanguage) {
      void this.translations.setLanguage(response.user.preferredLanguage);
    }
  }

  private clearSession(): void {
    this.currentUserState.set(null);
    this.accessTokenState.set(null);
  }
}
