import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RegisterPage } from './register-page';

describe('RegisterPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [RegisterPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  it('requires matching passwords with at least eight characters', () => {
    const page = TestBed.createComponent(RegisterPage).componentInstance;
    page.form.setValue({
      email: 'cook@example.com',
      password: 'short',
      confirmPassword: 'different',
    });

    expect(page.form.controls.password.hasError('minlength')).toBe(true);
    expect(page.form.hasError('passwordMismatch')).toBe(true);
  });
});
