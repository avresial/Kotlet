import { ChangeDetectionStrategy, Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { AiProviderService } from '../settings/ai-provider.service';
import { AgentMessage, AgentService } from './agent.service';

@Component({
  selector: 'app-agent-page', imports: [FormsModule, TranslatePipe],
  templateUrl: './agent-page.html', styleUrl: './agent-page.scss', changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgentPage {
  private readonly provider = inject(AiProviderService);
  private readonly agent = inject(AgentService);
  private readonly router = inject(Router);
  readonly models = signal<string[]>([]);
  readonly messages = signal<AgentMessage[]>([]);
  readonly sending = signal(false);
  model = '';
  prompt = '';
  @ViewChild('promptInput') promptInput?: ElementRef<HTMLTextAreaElement>;

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

  send(): void {
    const content = this.prompt.trim();
    if (!content || !this.model || this.sending()) return;
    const userMessage: AgentMessage = { role: 'user', content };
    const history = [...this.messages().filter(x => !x.error), userMessage];
    this.messages.update(x => [...x, userMessage]);
    this.prompt = ''; this.resetInput(); this.sending.set(true);
    this.agent.chat(this.model, history).pipe(finalize(() => this.sending.set(false))).subscribe({
      next: response => { this.append({ role: 'assistant', content: response.content }); },
      error: () => { this.append({ role: 'assistant', content: '', error: true }); },
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
    setTimeout(() => this.promptInput?.nativeElement.focus());
  }

  private resetInput(): void {
    const input = this.promptInput?.nativeElement;
    if (input) input.style.height = 'auto';
  }
}
