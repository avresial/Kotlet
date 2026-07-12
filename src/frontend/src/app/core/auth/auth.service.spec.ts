import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
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
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('stores the user after login', () => {
    service.login({ email: user.email, password: 'Password1!' }).subscribe();

    const request = http.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBe(true);
    request.flush(authResponse);

    expect(service.currentUser()).toEqual(user);
    expect(service.isAuthenticated()).toBe(true);
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
});
