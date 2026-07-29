import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from './auth.service';
import { CurrentUser } from './auth.models';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  const user: CurrentUser = {
    id: 'user-1',
    email: 'cook@example.com',
    displayName: null,
    preferredLanguage: null,
    theme: 'auto',
    createdAtUtc: '2026-06-27T00:00:00Z',
    lastLoginAtUtc: null,
    defaultHouseId: null,
    activeHouseId: null,
    hasHome: false,
    roles: ['User'],
  };
  const authResponse = {
    user,
    accessToken: 'access-token',
    accessTokenExpiresAtUtc: '2026-06-27T00:15:00Z',
    refreshToken: 'refresh-token',
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('stores the user after login', () => {
    service.login({ email: user.email, password: 'Password1!' }).subscribe();

    const request = http.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBe(true);
    request.flush(authResponse);

    expect(service.currentUser()).toEqual(user);
    expect(service.isAuthenticated()).toBe(true);
  });

  it('keeps the refresh token for browsers that block the cross-site cookie', async () => {
    service.login({ email: user.email, password: 'Password1!' }).subscribe();
    http.expectOne('/api/auth/login').flush(authResponse);
    expect(localStorage.getItem('kotlet.refreshToken')).toBe('refresh-token');

    const restoration = service.restoreSession();
    const refresh = http.expectOne('/api/auth/refresh');
    expect(refresh.request.headers.get('X-Refresh-Token')).toBe('refresh-token');
    refresh.flush({ ...authResponse, refreshToken: 'rotated-token' });
    await restoration;

    expect(localStorage.getItem('kotlet.refreshToken')).toBe('rotated-token');
  });

  it('drops the stored refresh token on logout', () => {
    service.login({ email: user.email, password: 'Password1!' }).subscribe();
    http.expectOne('/api/auth/login').flush(authResponse);

    service.logout().subscribe();
    const logout = http.expectOne('/api/auth/logout');
    expect(logout.request.headers.get('X-Refresh-Token')).toBe('refresh-token');
    logout.flush(null);

    expect(localStorage.getItem('kotlet.refreshToken')).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('restores a session through the refresh cookie', async () => {
    const restoration = service.restoreSession();
    http.expectOne('/api/auth/refresh').flush(authResponse);
    await restoration;

    expect(service.currentUser()).toEqual(user);
  });

  it('treats an unauthorized restoration as signed out', async () => {
    const restoration = service.restoreSession();
    http.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });
    await restoration;

    expect(service.isAuthenticated()).toBe(false);
  });

  it('waits for a sleeping API instead of signing the user out', async () => {
    vi.useFakeTimers();
    try {
      const restoration = service.restoreSession();
      http.expectOne('/api/auth/refresh').flush(null, { status: 503, statusText: 'Service Unavailable' });
      await vi.advanceTimersByTimeAsync(600);

      http.expectOne('/api/auth/refresh').flush(authResponse);
      await restoration;

      expect(service.isAuthenticated()).toBe(true);
      expect(service.apiUnreachable()).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('retries the restoration later when the API never woke up', async () => {
    vi.useFakeTimers();
    try {
      let settled = false;
      const restoration = service.restoreSession().then(() => (settled = true));
      for (let attempt = 0; attempt < 20 && !settled; attempt++) {
        http.expectOne('/api/auth/refresh').flush(null, { status: 504, statusText: 'Gateway Timeout' });
        await vi.advanceTimersByTimeAsync(30_000);
      }
      await restoration;

      expect(service.isAuthenticated()).toBe(false);
      expect(service.apiUnreachable()).toBe(true);

      // The give-up is not memoized: the next guard run asks again.
      const retry = service.restoreSession();
      http.expectOne('/api/auth/refresh').flush(authResponse);
      await retry;

      expect(service.isAuthenticated()).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });
});
