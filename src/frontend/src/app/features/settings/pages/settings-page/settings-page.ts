import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { finalize } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Language } from '../../../../core/i18n/language';
import { HomeService } from '../../../home/home.service';
import { HomeSummary } from '../../../home/home.models';
import { AiProviderService } from '../../ai-provider.service';
import { Theme } from '../../../../core/theme.service';

function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  return control.get('newPassword')?.value === control.get('confirmPassword')?.value ? null : { passwordMismatch: true };
}

function absoluteHttpUrl(control: AbstractControl): ValidationErrors | null {
  if (!control.value) return null;
  try {
    return ['http:', 'https:'].includes(new URL(control.value).protocol) ? null : { url: true };
  } catch {
    return { url: true };
  }
}

@Component({
  selector: 'app-settings-page',
  imports: [ButtonModule, DatePipe, InputTextModule, MessageModule, PasswordModule, ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsPage implements OnInit {
  readonly auth = inject(AuthService);
  private readonly translations = inject(TranslationService);
  private readonly homeService = inject(HomeService);
  private readonly aiProviderService = inject(AiProviderService);

  readonly homes = signal<HomeSummary[]>([]);

  readonly profileSaving = signal(false);
  readonly profileError = signal<string | null>(null);
  readonly profileSaved = signal(false);

  readonly passwordSaving = signal(false);
  readonly passwordError = signal<string | null>(null);
  readonly passwordSaved = signal(false);

  readonly providerConfigured = signal(false);
  readonly providerSaving = signal(false);
  readonly providerError = signal<string | null>(null);
  readonly providerSaved = signal(false);

  readonly profileForm = new FormGroup({
    displayName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    preferredLanguage: new FormControl<Language>('en', { nonNullable: true }),
    theme: new FormControl<Theme>('auto', { nonNullable: true }),
    defaultHouseId: new FormControl<string>('', { nonNullable: true }),
  });

  readonly passwordForm = new FormGroup(
    {
      currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      newPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }),
      confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    },
    { validators: passwordsMatch },
  );

  readonly providerForm = new FormGroup({
    providerName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    baseUrl: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2048), absoluteHttpUrl] }),
    apiKey: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(4096)] }),
    defaultModel: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
    isEnabled: new FormControl(false, { nonNullable: true }),
  });

  constructor() {
    effect(() => {
      const user = this.auth.currentUser();
      if (user) {
        this.profileForm.controls.displayName.setValue(user.displayName ?? '', { emitEvent: false });
        this.profileForm.controls.preferredLanguage.setValue(user.preferredLanguage ?? this.translations.language(), { emitEvent: false });
        this.profileForm.controls.theme.setValue(user.theme, { emitEvent: false });
        this.profileForm.controls.defaultHouseId.setValue(user.defaultHouseId ?? '', { emitEvent: false });
      }
    });
  }

  ngOnInit(): void {
    this.homeService.listHomes().subscribe({
      next: (homes) => this.homes.set(homes),
      error: () => this.homes.set([]),
    });
    this.aiProviderService.get().subscribe({
      next: (configuration) => {
        this.providerConfigured.set(configuration.hasApiKey);
        this.providerForm.patchValue({
          providerName: configuration.providerName,
          baseUrl: configuration.baseUrl,
          defaultModel: configuration.defaultModel ?? '',
          isEnabled: configuration.isEnabled,
          apiKey: '',
        });
      },
      error: (error: HttpErrorResponse) => {
        if (error.status !== 404)
          this.providerError.set(getApiError(error, this.translations.translate('settings.aiProviderLoadError')));
      },
    });
  }

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.profileSaving.set(true);
    this.profileError.set(null);
    this.profileSaved.set(false);
    const displayName = this.profileForm.controls.displayName.value.trim();
    const preferredLanguage = this.profileForm.controls.preferredLanguage.value;
    const theme = this.profileForm.controls.theme.value;
    const defaultHouseId = this.profileForm.controls.defaultHouseId.value || null;
    this.auth.updateProfile({ displayName: displayName.length > 0 ? displayName : null, preferredLanguage, defaultHouseId, theme })
      .pipe(finalize(() => this.profileSaving.set(false)))
      .subscribe({
        next: () => {
          void this.translations.setLanguage(preferredLanguage);
          this.profileSaved.set(true);
        },
        error: (error) => this.profileError.set(getApiError(error, this.translations.translate('settings.profileError'))),
      });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.passwordSaving.set(true);
    this.passwordError.set(null);
    this.passwordSaved.set(false);
    this.auth.changePassword(this.passwordForm.getRawValue())
      .pipe(finalize(() => this.passwordSaving.set(false)))
      .subscribe({
        next: () => {
          this.passwordSaved.set(true);
          this.passwordForm.reset();
        },
        error: (error) => this.passwordError.set(getApiError(error, this.translations.translate('settings.passwordError'))),
      });
  }

  saveProvider(): void {
    const { baseUrl, apiKey, isEnabled } = this.providerForm.controls;
    baseUrl.updateValueAndValidity();
    apiKey.updateValueAndValidity();
    if (isEnabled.value && !baseUrl.value.trim()) baseUrl.setErrors({ ...baseUrl.errors, required: true });
    if (isEnabled.value && !apiKey.value && !this.providerConfigured()) apiKey.setErrors({ ...apiKey.errors, required: true });
    if (this.providerForm.invalid) {
      this.providerForm.markAllAsTouched();
      return;
    }

    this.providerSaving.set(true);
    this.providerError.set(null);
    this.providerSaved.set(false);
    const value = this.providerForm.getRawValue();
    this.aiProviderService.save({
      providerName: value.providerName.trim(),
      baseUrl: value.baseUrl.trim(),
      defaultModel: value.defaultModel.trim() || null,
      isEnabled: value.isEnabled,
      ...(value.apiKey ? { apiKey: value.apiKey } : {}),
    }).pipe(finalize(() => this.providerSaving.set(false))).subscribe({
      next: (configuration) => {
        this.providerConfigured.set(configuration.hasApiKey);
        this.providerForm.controls.apiKey.reset();
        this.providerSaved.set(true);
      },
      error: (error) => this.providerError.set(getApiError(error, this.translations.translate('settings.aiProviderError'))),
    });
  }

  deleteProvider(): void {
    if (!confirm(this.translations.translate('settings.aiProviderDeleteConfirm'))) return;
    this.providerSaving.set(true);
    this.providerError.set(null);
    this.aiProviderService.delete().pipe(finalize(() => this.providerSaving.set(false))).subscribe({
      next: () => {
        this.providerConfigured.set(false);
        this.providerSaved.set(false);
        this.providerForm.reset();
      },
      error: (error) => this.providerError.set(getApiError(error, this.translations.translate('settings.aiProviderDeleteError'))),
    });
  }
}
