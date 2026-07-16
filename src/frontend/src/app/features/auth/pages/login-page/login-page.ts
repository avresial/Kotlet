import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { finalize } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-login-page',
  imports: [ButtonModule, InputTextModule, MessageModule, PasswordModule, ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './login-page.html',
  styleUrl: './login-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translations = inject(TranslationService);

  readonly isLoading = signal(false);
  readonly isOAuthRedirect = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  login(): void {
    if (this.isLoading()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.auth.login(this.form.getRawValue()).pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: (response) => {
        const returnUrl = this.safeReturnUrl();
        if (returnUrl.startsWith('http')) {
          this.isOAuthRedirect.set(true);
          this.completeOAuthLogin(response.accessToken, returnUrl);
          return;
        }
        void this.router.navigateByUrl(returnUrl);
      },
      error: (error) => this.errorMessage.set(getApiError(error, this.translations.translate('auth.login.error'))),
    });
  }

  /**
   * Hands the OAuth authorization session to the API origin as a first-party cookie. The login
   * fetch above runs cross-site (this SPA and the API are on different sites), so the refresh
   * cookie it sets is a third-party cookie that mobile browsers drop — leaving
   * <c>/connect/authorize</c> with no session and no way back to the calling app. Submitting a
   * top-level POST to the bridge makes the navigation first-party to the API, so the cookie it
   * sets sticks; the bridge then redirects on to the original authorize URL.
   */
  private completeOAuthLogin(accessToken: string, returnUrl: string): void {
    const bridgeUrl = `${new URL(returnUrl).origin}/api/auth/oauth-bridge`;
    const form = document.createElement('form');
    form.method = 'POST';
    form.action = bridgeUrl;
    form.hidden = true;
    for (const [name, value] of [['token', accessToken], ['returnUrl', returnUrl]]) {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = name;
      input.value = value;
      form.appendChild(input);
    }
    document.body.appendChild(form);
    form.submit();
  }

  private safeReturnUrl(): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (returnUrl?.startsWith('/') && !returnUrl.startsWith('//')) {
      return returnUrl;
    }
    try {
      const target = new URL(returnUrl ?? '');
      const api = new URL(environment.apiBaseUrl);
      if (target.origin === api.origin && target.pathname === '/connect/authorize') {
        return target.href;
      }
    } catch {
      // Invalid and relative URLs fall back to the dashboard.
    }
    return '/dashboard';
  }
}
