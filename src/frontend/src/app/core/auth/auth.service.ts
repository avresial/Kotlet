import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, tap } from 'rxjs';
import { AuthResponse, CurrentUser, LoginRequest, RegisterRequest } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly currentUserState = signal<CurrentUser | null>(null);

  readonly currentUser = this.currentUserState.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserState() !== null);

  async restoreSession(): Promise<void> {
    try {
      const user = await firstValueFrom(
        this.http.get<CurrentUser>('/api/auth/me', { withCredentials: true }),
      );
      this.currentUserState.set(user);
    } catch (error) {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        console.error('Unable to restore the session.', error);
      }
      this.currentUserState.set(null);
    }
  }

  login(request: LoginRequest) {
    return this.http
      .post<AuthResponse>('/api/auth/login', request, { withCredentials: true })
      .pipe(tap(({ user }) => this.currentUserState.set(user)));
  }

  register(request: RegisterRequest) {
    return this.http
      .post<AuthResponse>('/api/auth/register', request, { withCredentials: true })
      .pipe(tap(({ user }) => this.currentUserState.set(user)));
  }

  logout() {
    return this.http
      .post<void>('/api/auth/logout', null, { withCredentials: true })
      .pipe(tap(() => this.currentUserState.set(null)));
  }
}
