import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
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

function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  return control.get('newPassword')?.value === control.get('confirmPassword')?.value ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-settings-page',
  imports: [ButtonModule, DatePipe, InputTextModule, MessageModule, PasswordModule, ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsPage {
  readonly auth = inject(AuthService);
  private readonly translations = inject(TranslationService);

  readonly profileSaving = signal(false);
  readonly profileError = signal<string | null>(null);
  readonly profileSaved = signal(false);

  readonly passwordSaving = signal(false);
  readonly passwordError = signal<string | null>(null);
  readonly passwordSaved = signal(false);

  readonly profileForm = new FormGroup({
    displayName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    preferredLanguage: new FormControl<Language>('en', { nonNullable: true }),
  });

  readonly passwordForm = new FormGroup(
    {
      currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      newPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }),
      confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    },
    { validators: passwordsMatch },
  );

  constructor() {
    effect(() => {
      const user = this.auth.currentUser();
      if (user) {
        this.profileForm.controls.displayName.setValue(user.displayName ?? '', { emitEvent: false });
        this.profileForm.controls.preferredLanguage.setValue(user.preferredLanguage ?? this.translations.language(), { emitEvent: false });
      }
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
    this.auth.updateProfile({ displayName: displayName.length > 0 ? displayName : null, preferredLanguage })
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
}
