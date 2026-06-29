import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
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
