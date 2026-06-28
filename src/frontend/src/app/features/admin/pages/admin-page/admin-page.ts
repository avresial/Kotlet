import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { finalize } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { AdminUser } from '../../admin.models';
import { AdminService } from '../../admin.service';

@Component({
  selector: 'app-admin-page',
  imports: [ButtonModule, DatePipe, InputTextModule, MessageModule, ReactiveFormsModule, RouterLink, TranslatePipe],
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
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly deletingId = signal<string | null>(null);
  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    displayName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    preferredLanguage: new FormControl<'en' | 'pl' | ''>('', { nonNullable: true }),
  });

  constructor() { this.loadUsers(); }

  edit(user: AdminUser): void {
    this.editingId.set(user.id);
    this.form.setValue({ email: user.email, displayName: user.displayName ?? '', preferredLanguage: user.preferredLanguage ?? '' });
  }

  cancel(): void { this.editingId.set(null); this.form.reset(); this.error.set(null); }

  save(): void {
    const id = this.editingId();
    if (!id || this.form.invalid) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    this.saving.set(true); this.error.set(null);
    this.admin.updateUser(id, { email: value.email.trim(), displayName: value.displayName.trim() || null, preferredLanguage: value.preferredLanguage || null })
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
        this.users.update((users) => users.filter((item) => item.id !== user.id));
        if (this.auth.currentUser()?.id === user.id) {
          this.auth.logout().subscribe({ next: () => void this.router.navigateByUrl('/login') });
        }
      },
      error: (error) => this.error.set(getApiError(error, this.translations.translate('admin.deleteError'))),
    });
  }

  private loadUsers(): void {
    this.loading.set(true); this.error.set(null);
    this.admin.getUsers().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (users) => this.users.set(users),
      error: (error) => this.error.set(getApiError(error, this.translations.translate('admin.loadError'))),
    });
  }
}
