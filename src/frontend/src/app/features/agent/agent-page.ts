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
  readonly error = signal<string | null>(null);
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
    const history = [...this.messages(), { role: 'user' as const, content }];
    this.messages.set(history); this.prompt = ''; this.error.set(null); this.sending.set(true);
    this.agent.chat(this.model, history).pipe(finalize(() => this.sending.set(false))).subscribe({
      next: response => { this.messages.update(x => [...x, { role: 'assistant', content: response.content }]); setTimeout(() => this.promptInput?.nativeElement.focus()); },
      error: () => this.error.set('The agent could not answer. Check your provider and model settings.'),
    });
  }

  keydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); this.send(); }
  }
}
