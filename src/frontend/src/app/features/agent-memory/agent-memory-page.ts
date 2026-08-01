import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { finalize } from 'rxjs';
import { getApiError } from '../../core/http/api-error';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import { AgentMemory, AgentMemoryDraft, AgentMemoryService } from './agent-memory.service';

@Component({
  selector: 'app-agent-memory-page',
  imports: [ButtonModule, DatePipe, FormsModule, InputTextModule, MessageModule, ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './agent-memory-page.html',
  styleUrl: './agent-memory-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgentMemoryPage implements OnInit {
  private readonly service = inject(AgentMemoryService);
  private readonly translations = inject(TranslationService);

  readonly memories = signal<AgentMemory[]>([]);
  readonly revision = signal(0);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly search = signal('');
  readonly editingId = signal<string | null>(null);
  readonly markdown = signal<string | null>(null);
  readonly visibleMemories = computed(() => {
    const query = this.search().trim().toLocaleLowerCase();
    return query
      ? this.memories().filter(memory => `${memory.title ?? ''} ${memory.content} ${memory.category ?? ''}`.toLocaleLowerCase().includes(query))
      : this.memories();
  });

  readonly form = new FormGroup({
    title: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
    content: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(10000)] }),
    category: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    source: new FormControl('ExplicitRequest', { nonNullable: true }),
    reviewStatus: new FormControl('Confirmed', { nonNullable: true }),
    expiresAt: new FormControl('', { nonNullable: true }),
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.list().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => { this.memories.set(result.memories); this.revision.set(result.revision); },
      error: error => this.error.set(getApiError(error, this.translations.translate('memory.loadError'))),
    });
  }

  beginCreate(): void {
    this.editingId.set(null);
    this.form.reset({ title: '', content: '', category: '', source: 'ExplicitRequest', reviewStatus: 'Confirmed', expiresAt: '' });
  }

  beginEdit(memory: AgentMemory): void {
    this.editingId.set(memory.id);
    this.form.reset({
      title: memory.title ?? '', content: memory.content, category: memory.category ?? '',
      source: memory.source, reviewStatus: memory.reviewStatus,
      expiresAt: memory.expiresAt ? memory.expiresAt.slice(0, 10) : '',
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  cancelEdit(): void { this.beginCreate(); }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    const editing = this.memories().find(memory => memory.id === this.editingId());
    const draft: AgentMemoryDraft = {
      content: value.content.trim(), title: value.title.trim() || null, category: value.category.trim() || null,
      source: value.source, reviewStatus: value.reviewStatus, confidence: editing?.confidence ?? null,
      expiresAt: value.expiresAt ? new Date(`${value.expiresAt}T23:59:59Z`).toISOString() : null, clientId: 'web',
    };
    const request = editing ? this.service.update(editing.id, draft, editing.version) : this.service.create(draft);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: result => {
        if (result.status !== 'Success' || !result.memory) { this.error.set(result.message ?? this.translations.translate('memory.saveError')); return; }
        this.editingId.set(null);
        this.load();
        this.beginCreate();
      },
      error: error => this.error.set(getApiError(error, this.translations.translate('memory.saveError'))),
    });
  }

  remove(memory: AgentMemory): void {
    if (this.saving()) return;
    if (!confirm(this.translations.translate('memory.deleteConfirm'))) return;
    this.saving.set(true);
    this.service.delete(memory.id, memory.version).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => this.load(),
      error: error => this.error.set(getApiError(error, this.translations.translate('memory.deleteError'))),
    });
  }

  setReviewStatus(memory: AgentMemory, reviewStatus: string): void {
    if (this.saving()) return;
    this.saving.set(true);
    const draft: AgentMemoryDraft = {
      content: memory.content, title: memory.title, category: memory.category, source: memory.source,
      confidence: memory.confidence, reviewStatus, expiresAt: memory.expiresAt, clientId: 'web',
    };
    this.service.update(memory.id, draft, memory.version).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => this.load(),
      error: error => this.error.set(getApiError(error, this.translations.translate('memory.saveError'))),
    });
  }

  exportMarkdown(): void {
    this.service.exportMarkdown().subscribe({
      next: result => {
        this.markdown.set(result.markdown);
        const link = document.createElement('a');
        const url = URL.createObjectURL(new Blob([result.markdown], { type: 'text/markdown' }));
        link.href = url;
        link.download = 'kotlet-agent-memory.md';
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 0);
      },
      error: error => this.error.set(getApiError(error, this.translations.translate('memory.exportError'))),
    });
  }
}
