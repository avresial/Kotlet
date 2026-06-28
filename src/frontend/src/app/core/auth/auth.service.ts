import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, tap } from 'rxjs';
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

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly translations = inject(TranslationService);
  private readonly currentUserState = signal<CurrentUser | null>(null);
  private readonly accessTokenState = signal<string | null>(null);
  private restoration: Promise<void> | null = null;

  readonly currentUser = this.currentUserState.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserState() !== null);
  readonly accessToken = this.accessTokenState.asReadonly();

  /**
   * Attempts to restore the session from the refresh-token cookie. The work is
   * memoized so the app initializer and the route guards share a single refresh
   * call: guards await this promise before deciding, which prevents a redirect
   * to the login page while the refresh request is still in flight on reload.
   */
  restoreSession(): Promise<void> {
    return (this.restoration ??= this.restoreFromRefreshToken());
  }

  private async restoreFromRefreshToken(): Promise<void> {
    try {
      const response = await firstValueFrom(this.refresh());
      this.setSession(response);
    } catch (error) {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        console.error('Unable to restore the session.', error);
      }
      this.currentUserState.set(null);
      this.accessTokenState.set(null);
    }
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

  refresh() {
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
      .pipe(tap(() => {
        this.currentUserState.set(null);
        this.accessTokenState.set(null);
      }));
  }

  setSession(response: AuthResponse): void {
    this.currentUserState.set(response.user);
    this.accessTokenState.set(response.accessToken);
    if (response.user.preferredLanguage) {
      void this.translations.setLanguage(response.user.preferredLanguage);
    }
  }
}
