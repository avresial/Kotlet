import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { finalize } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { AdminUser } from '../../admin.models';
import { AdminService } from '../../admin.service';

@Component({
  selector: 'app-admin-page',
  imports: [ButtonModule, DatePipe, InputTextModule, MessageModule, PasswordModule, ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './admin-page.html',
  styleUrl: './admin-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPage {
  private readonly admin = inject(AdminService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly translations = inject(TranslationService);
  readonly users = signal<AdminUser[]>([]);
  readonly search = signal('');
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 10;
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly deletingId = signal<string | null>(null);
  readonly transcriptConfigured = signal(false);
  readonly transcriptEditing = signal(false);
  readonly transcriptSaving = signal(false);
  readonly transcriptSaved = signal(false);
  readonly transcriptError = signal<string | null>(null);
  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    displayName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    preferredLanguage: new FormControl<'en' | 'pl' | ''>('', { nonNullable: true }),
    userRole: new FormControl(false, { nonNullable: true }),
    adminRole: new FormControl(false, { nonNullable: true }),
  });
  readonly transcriptForm = new FormGroup({
    apiKey: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(4096)] }),
  });

  constructor() {
    this.loadUsers();
    this.loadTranscriptSettings();
  }

  configureTranscript(): void {
    this.transcriptEditing.set(true);
    this.transcriptSaved.set(false);
    this.transcriptError.set(null);
    this.transcriptForm.reset();
  }

  cancelTranscriptConfiguration(): void {
    this.transcriptEditing.set(false);
    this.transcriptError.set(null);
    this.transcriptForm.reset();
  }

  saveTranscriptConfiguration(): void {
    if (this.transcriptForm.invalid || (!this.transcriptConfigured() && !this.transcriptForm.controls.apiKey.value.trim())) {
      this.transcriptForm.markAllAsTouched();
      return;
    }

    this.transcriptSaving.set(true);
    this.transcriptSaved.set(false);
    this.transcriptError.set(null);
    this.admin.saveYoutubeTranscriptionSettings(this.transcriptForm.controls.apiKey.value.trim() || null)
      .pipe(finalize(() => this.transcriptSaving.set(false)))
      .subscribe({
        next: (settings) => {
          this.transcriptConfigured.set(settings.hasApiKey);
          this.transcriptEditing.set(false);
          this.transcriptSaved.set(true);
          this.transcriptForm.reset();
        },
        error: (error) => this.transcriptError.set(getApiError(error, this.translations.translate('admin.apiKeySaveError'))),
      });
  }

  edit(user: AdminUser): void {
    this.editingId.set(user.id);
    this.form.setValue({
      email: user.email,
      displayName: user.displayName ?? '',
      preferredLanguage: user.preferredLanguage ?? '',
      userRole: user.roles.includes('User'),
      adminRole: user.roles.includes('Admin'),
    });
  }

  cancel(): void { this.editingId.set(null); this.form.reset(); this.error.set(null); }

  save(): void {
    const id = this.editingId();
    if (!id || this.form.invalid) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    this.saving.set(true); this.error.set(null);
    const roles = [value.userRole && 'User', value.adminRole && 'Admin'].filter((role): role is string => Boolean(role));
    this.admin.updateUser(id, { email: value.email.trim(), displayName: value.displayName.trim() || null, preferredLanguage: value.preferredLanguage || null, roles })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (user) => { this.users.update((users) => users.map((item) => item.id === user.id ? user : item)); this.cancel(); },
        error: (error) => this.error.set(getApiError(error, this.translations.translate('admin.saveError'))),
      });
  }

  remove(user: AdminUser): void {
    if (!confirm(this.translations.translate('admin.deleteConfirm').replace('{name}', user.displayName || user.email))) return;
    this.deletingId.set(user.id); this.error.set(null);
    this.admin.deleteUser(user.id).pipe(finalize(() => this.deletingId.set(null))).subscribe({
      next: () => {
        if (this.auth.currentUser()?.id === user.id) {
          this.auth.logout().subscribe({ next: () => void this.router.navigateByUrl('/login') });
          return;
        }
        const remaining = this.totalCount() - 1;
        if (this.page() > 1 && (this.page() - 1) * this.pageSize >= remaining) this.page.update((page) => page - 1);
        this.loadUsers();
      },
      error: (error) => this.error.set(getApiError(error, this.translations.translate('admin.deleteError'))),
    });
  }

  searchUsers(): void { this.page.set(1); this.loadUsers(); }
  clearSearch(): void { this.search.set(''); this.searchUsers(); }
  previousPage(): void { if (this.page() > 1) { this.page.update((page) => page - 1); this.loadUsers(); } }
  nextPage(): void { if (this.hasNextPage) { this.page.update((page) => page + 1); this.loadUsers(); } }
  get hasNextPage(): boolean { return this.page() * this.pageSize < this.totalCount(); }

  private loadUsers(): void {
    this.loading.set(true); this.error.set(null);
    this.admin.getUsers(this.page(), this.search().trim() || undefined).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (response) => { this.users.set(response.items); this.totalCount.set(response.totalCount); },
      error: (error) => this.error.set(getApiError(error, this.translations.translate('admin.loadError'))),
    });
  }

  private loadTranscriptSettings(): void {
    this.admin.getYoutubeTranscriptionSettings().subscribe({
      next: (settings) => this.transcriptConfigured.set(settings.hasApiKey),
      error: (error) => this.transcriptError.set(getApiError(error, this.translations.translate('admin.apiKeyLoadError'))),
    });
  }
}
