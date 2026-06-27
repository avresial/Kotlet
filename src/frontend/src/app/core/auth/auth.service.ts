import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, tap } from 'rxjs';
import { AuthResponse, CurrentUser, LoginRequest, RegisterRequest } from './auth.models';
import { apiUrl } from '../http/api-url';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly currentUserState = signal<CurrentUser | null>(null);
  private readonly accessTokenState = signal<string | null>(null);

  readonly currentUser = this.currentUserState.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserState() !== null);
  readonly accessToken = this.accessTokenState.asReadonly();

  async restoreSession(): Promise<void> {
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
  }
}
