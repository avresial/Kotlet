import { ChangeDetectionStrategy, Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { finalize } from 'rxjs';
import DOMPurify from 'dompurify';
import { marked } from 'marked';
import { getApiError } from '../../core/http/api-error';
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
    const userMessage: AgentMessage = { role: 'user', content };
    const history = [...this.messages().filter(x => !x.error).map(({ role, content }) => ({ role, content })), { role: 'user' as const, content }];
    this.messages.update(x => [...x, userMessage]);
    this.prompt = ''; this.resetInput(); this.sending.set(true);
    this.scrollToBottom();
    const startTime = Date.now();
    this.agent.chat(selectedModel, history).pipe(finalize(() => this.sending.set(false))).subscribe({
      next: response => { this.append({ role: 'assistant', content: response.content.trim(), model: selectedModel, responseTimeMs: Date.now() - startTime }); },
      error: error => { this.append({ role: 'assistant', content: getApiError(error, 'The agent could not answer. Check your provider and model settings.'), error: true }); },
    });
  }

  keydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); this.send(); }
  }

  autogrow(): void {
    const input = this.promptInput?.nativeElement;
    if (!input) return;
    input.style.height = 'auto';
    input.style.height = `${Math.min(input.scrollHeight, 200)}px`;
  }

  private append(message: AgentMessage): void {
    this.messages.update(x => [...x, message]);
    this.scrollToBottom();
    setTimeout(() => this.promptInput?.nativeElement.focus());
  }

  private resetInput(): void {
    const input = this.promptInput?.nativeElement;
    if (input) input.style.height = 'auto';
  }
}
