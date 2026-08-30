import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { ToolsService } from '../../services/tools.service';

@Component({
  selector: 'app-tools-hub-page',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="tools-hub-page">
      <header>
        <p class="eyebrow">Kitchen Utilities</p>
        <h1>Tools Hub</h1>
        <p>Smart tools to streamline recipe extraction, video transcription, and smart meal planning.</p>
      </header>

      <section class="featured-tool-card">
        <div class="card-header">
          <div>
            <span class="ai-badge">AI-Assisted</span>
            <h2>Video Transcription</h2>
          </div>
          <span class="icon" aria-hidden="true">🎬</span>
        </div>
        <p class="card-description">
          Transcribe YouTube or TikTok cooking videos, extract ingredients with backend confidence matching, and convert transcripts directly into editable recipe drafts.
        </p>

        <form class="quick-start-form" (ngSubmit)="startTranscription()">
          <label for="hub-video-url">Video URL</label>
          <div class="url-input-group">
            <input
              id="hub-video-url"
              type="url"
              name="url"
              [ngModel]="url()"
              (ngModelChange)="url.set($event)"
              placeholder="https://www.youtube.com/watch?v=… or https://tiktok.com/@…"
              autocomplete="url"
            />
            <button type="submit" [disabled]="!isValidUrl() || isStarting()">
              {{ isStarting() ? 'Starting…' : 'Transcribe' }}
            </button>
          </div>
          @if (url() && !isValidUrl()) {
            <p class="field-error">Enter a valid YouTube or TikTok video link.</p>
          }
        </form>

        <div class="card-footer">
          <a routerLink="/tools/video-transcription" class="secondary-link">Open Transcriber Workflow →</a>
        </div>

        @if (error()) {
          <div class="error" role="alert">{{ error() }}</div>
        }
      </section>

      <section class="tools-grid">
        <div class="tool-card">
          <div class="tool-icon">📝</div>
          <h3>Recipe Import Reviewer</h3>
          <p>Inspect, edit, and refine recipe drafts extracted from video transcriptions before saving to your collection.</p>
          <a routerLink="/recipes/import" class="tool-link">Go to Recipe Imports →</a>
        </div>

        <div class="tool-card">
          <div class="tool-icon">🥫</div>
          <h3>Pantry Matcher</h3>
          <p>Analyze your current kitchen inventory and find recipes you can make immediately without extra shopping.</p>
          <a routerLink="/pantry" class="tool-link">Open Pantry →</a>
        </div>

        <div class="tool-card">
          <div class="tool-icon">📅</div>
          <h3>Meal Planner</h3>
          <p>Plan weekly meals and auto-generate grocery lists tailored to your household's dietary preferences.</p>
          <a routerLink="/meal-planner" class="tool-link">Open Planner →</a>
        </div>
      </section>
    </main>
  `,
  styles: [`
    .tools-hub-page { width: min(56rem, calc(100% - 2.5rem)); margin: 0 auto; padding: 2.5rem 0 4rem; color: var(--app-text); }
    header { margin-bottom: 2.5rem; h1 { margin: 0.4rem 0; font: 500 clamp(2.2rem, 5vw, 3.5rem) / 1.05 Georgia, serif; } p { color: var(--app-text-muted); font-size: 1.1rem; } }
    .eyebrow { margin: 0; color: #9a3a2a; font-size: 0.72rem; font-weight: 800; letter-spacing: 0.13em; text-transform: uppercase; }
    .featured-tool-card { padding: 2rem; border: 1px solid var(--app-border); border-radius: 1.25rem; background: var(--app-surface); box-shadow: 0 0.7rem 2rem rgb(70 52 31 / 6%); margin-bottom: 3rem; }
    .card-header { display: flex; justify-content: space-between; align-items: flex-start; h2 { margin: 0.5rem 0 0; font: 500 2rem Georgia, serif; } .icon { font-size: 2.5rem; } }
    .ai-badge { display: inline-block; padding: 0.25rem 0.6rem; border-radius: 999px; background: var(--app-ai-bg); color: var(--app-ai); font-size: 0.78rem; font-weight: 800; }
    .card-description { margin: 1rem 0 1.5rem; color: var(--app-text-muted); line-height: 1.6; }
    .quick-start-form { label { display: block; margin-bottom: 0.4rem; font-weight: 700; } }
    .url-input-group { display: grid; grid-template-columns: 1fr auto; gap: 0.6rem; input { box-sizing: border-box; width: 100%; padding: 0.8rem 1rem; border: 1px solid var(--app-border-strong); border-radius: 0.55rem; background: var(--app-surface-raised); font: inherit; &:focus { outline: 2px solid #b96554; outline-offset: 1px; } } button { padding: 0.8rem 1.4rem; border: 0; border-radius: 0.55rem; background: #963827; color: white; font: inherit; font-weight: 700; cursor: pointer; transition: background-color 0.15s ease; &:hover:not(:disabled) { background: #7d2e1f; } &:disabled { opacity: 0.5; cursor: not-allowed; } } }
    .field-error { margin-top: 0.4rem; color: var(--app-danger); font-size: 0.9rem; }
    .error { margin-top: 1rem; padding: 0.8rem 1rem; border: 1px solid var(--app-danger-border); border-radius: 0.6rem; background: var(--app-danger-bg); color: var(--app-danger); }
    .card-footer { margin-top: 1.5rem; padding-top: 1rem; border-top: 1px solid var(--app-border); .secondary-link { color: #7d3428; font-weight: 700; text-decoration: none; &:hover { text-decoration: underline; } } }
    .tools-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr)); gap: 1.5rem; }
    .tool-card { padding: 1.5rem; border: 1px solid var(--app-border); border-radius: 1rem; background: var(--app-surface); box-shadow: 0 0.4rem 1.2rem rgb(70 52 31 / 4%); display: flex; flex-direction: column; .tool-icon { font-size: 1.8rem; margin-bottom: 0.6rem; } h3 { margin: 0 0 0.5rem; font-size: 1.25rem; } p { margin: 0 0 1.25rem; color: var(--app-text-muted); font-size: 0.95rem; line-height: 1.5; flex-grow: 1; } .tool-link { color: #7d3428; font-weight: 700; text-decoration: none; &:hover { text-decoration: underline; } } }
    @media (max-width: 42rem) {
      .tools-hub-page { width: 100%; box-sizing: border-box; padding: 1rem .75rem 2rem; }
      header { margin-bottom: 1rem; h1 { margin: .3rem 0; font-size: var(--density-title); } p { margin: 0; font-size: .9rem; line-height: 1.3; } }
      .featured-tool-card { margin-inline: -.75rem; margin-bottom: .5rem; padding: .75rem; border-inline: 0; border-radius: 0; box-shadow: none; }
      .card-header { h2 { margin-top: .3rem; font-size: 1.25rem; } .icon { font-size: 1.5rem; } }
      .card-description { margin: .5rem 0 .75rem; font-size: .85rem; line-height: 1.35; }
      .url-input-group { grid-template-columns: 1fr; gap: .4rem; input, button { min-height: var(--density-control); padding: .55rem .7rem; } }
      .card-footer { margin-top: .75rem; padding-top: .6rem; }
      .tools-grid { margin-inline: -.75rem; grid-template-columns: 1fr; gap: .35rem; }
      .tool-card { min-height: var(--density-row); padding: .6rem .75rem; border-inline: 0; border-radius: 0; display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: .5rem; box-shadow: none; }
      .tool-card .tool-icon { margin: 0; font-size: 1.25rem; }
      .tool-card h3 { margin: 0; font-size: 1rem; }
      .tool-card p { display: none; }
      .tool-card .tool-link { font-size: 0; }
      .tool-card .tool-link::after { content: '→'; font-size: 1rem; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToolsHubPage {
  private readonly toolsService = inject(ToolsService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly url = signal('');
  readonly isStarting = signal(false);
  readonly error = signal<string | null>(null);

  readonly isValidUrl = computed(() => this.isSupportedVideoUrl(this.url()));

  startTranscription(): void {
    if (!this.isValidUrl() || this.isStarting()) return;
    this.isStarting.set(true);
    this.error.set(null);

    this.toolsService
      .createVideoTranscription(this.url().trim())
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.isStarting.set(false))
      )
      .subscribe({
        next: ({ id }) => {
          this.router.navigate(['/tools/video-transcription', id]);
        },
        error: (err) => {
          this.error.set(getApiError(err, 'Could not start video transcription.'));
        },
      });
  }

  isSupportedVideoUrl(value: string): boolean {
    try {
      const parsed = new URL(value.trim());
      if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') return false;
      return /(^|\.)(youtube\.com|youtu\.be|tiktok\.com)$/i.test(parsed.hostname);
    } catch {
      return false;
    }
  }
}
