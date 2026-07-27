import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  it('refreshes once when several requests fail with 401 at the same time', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        AuthService,
      ],
    });
    const http = TestBed.inject(HttpClient);
    const controller = TestBed.inject(HttpTestingController);

    http.get('/api/pantry').subscribe({ error: () => undefined });
    http.get('/api/recipes').subscribe({ error: () => undefined });
    for (const request of controller.match('/api/pantry').concat(controller.match('/api/recipes')))
      request.flush(null, { status: 401, statusText: 'Unauthorized' });

    // A second, parallel refresh would present the rotated cookie and make the API revoke the
    // whole token family, signing the user out of every device.
    const refreshes = controller.match('/api/auth/refresh');
    expect(refreshes.length).toBe(1);

    refreshes[0].flush({
      user: { id: 'user-1', preferredLanguage: null },
      accessToken: 'fresh-token',
      accessTokenExpiresAtUtc: '2026-06-27T00:15:00Z',
    });
    for (const request of controller.match('/api/pantry').concat(controller.match('/api/recipes')))
      request.flush({});
    controller.verify();
  });

  it('does not send credentials to external services', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { accessToken: () => 'secret-token' } },
      ],
    });

    TestBed.inject(HttpClient).get('https://example.com/fact').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne('https://example.com/fact');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });
});
