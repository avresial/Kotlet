import { ChangeDetectionStrategy, Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { finalize } from 'rxjs';
import DOMPurify from 'dompurify';
import { marked } from 'marked';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { AiProviderService } from '../settings/ai-provider.service';
import { AgentMessage, AgentService } from './agent.service';

@Component({
  selector: 'app-agent-page', imports: [FormsModule, CommonModule, TranslatePipe],
  templateUrl: './agent-page.html', styleUrl: './agent-page.scss', changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgentPage {
  private readonly provider = inject(AiProviderService);
  private readonly agent = inject(AgentService);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  readonly models = signal<string[]>([]);
  readonly messages = signal<AgentMessage[]>([]);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);
  model = '';
  prompt = '';
  @ViewChild('promptInput') promptInput?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('conversation') conversation?: ElementRef<HTMLElement>;

  constructor() {
    this.provider.get().subscribe({
      next: config => {
        if (!config.isEnabled || !config.hasApiKey || !config.models.length) return void this.router.navigateByUrl('/settings');
        this.models.set(config.models);
        this.model = config.defaultModel && config.models.includes(config.defaultModel) ? config.defaultModel : config.models[0];
      },
      error: () => void this.router.navigateByUrl('/settings'),
    });
  }

  renderMarkdown(content: string): SafeHtml {
    const trimmed = content.trim();
    if (!trimmed) return '';
    const raw = marked.parse(trimmed, { async: false }) as string;
    const clean = DOMPurify.sanitize(raw);
    return this.sanitizer.bypassSecurityTrustHtml(clean);
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.conversation?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }

  send(): void {
    const content = this.prompt.trim();
    if (!content || !this.model || this.sending()) return;
    const selectedModel = this.model;
    const userMsg = { role: 'user' as const, content };
    const historyForApi = [...this.messages().map(({ role, content }) => ({ role, content })), userMsg];
    this.messages.set([...this.messages(), userMsg]);
    this.prompt = '';
    this.error.set(null);
    this.sending.set(true);
    this.scrollToBottom();
    const startTime = Date.now();
    this.agent.chat(selectedModel, historyForApi).pipe(finalize(() => this.sending.set(false))).subscribe({
      next: response => {
        const responseTimeMs = Date.now() - startTime;
        const assistantContent = response.content.trim();
        this.messages.update(x => [...x, { role: 'assistant', content: assistantContent, model: selectedModel, responseTimeMs }]);
        this.scrollToBottom();
        setTimeout(() => this.promptInput?.nativeElement.focus());
      },
      error: () => this.error.set('The agent could not answer. Check your provider and model settings.'),
    });
  }

  keydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); this.send(); }
  }
}
