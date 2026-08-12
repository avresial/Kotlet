import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, switchMap, takeWhile, timer } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import {
  DetectedIngredient,
  IngredientConfidenceState,
  VideoTranscriptionJob,
  VideoTranscriptionStatus,
} from '../../models/tools.models';
import { ToolsService } from '../../services/tools.service';

@Component({
  selector: 'app-video-transcription-page',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="transcription-page">
      <a class="back-link" routerLink="/tools">← Back to Tools Hub</a>

      <header>
        <div class="title-row">
          <span class="ai-badge">AI-Assisted</span>
          <h1>Video Transcription</h1>
        </div>
        <p>Extract transcripts and ingredients from YouTube or TikTok cooking videos.</p>
      </header>

      @if (!job()) {
        <form class="start-card" (ngSubmit)="start()">
          <label for="video-url">Video URL</label>
          <div class="url-row">
            <input
              id="video-url"
              type="url"
              name="url"
              [ngModel]="url()"
              (ngModelChange)="url.set($event)"
              placeholder="https://www.youtube.com/watch?v=… or https://tiktok.com/@…"
              autocomplete="url"
              required
            />
            <button type="submit" [disabled]="!isValidUrl() || isStarting()">
              {{ isStarting() ? 'Starting…' : 'Transcribe' }}
            </button>
          </div>
          @if (url() && !isValidUrl()) {
            <p class="field-error">Enter a valid YouTube or TikTok URL.</p>
          }
        </form>
      } @else if (hasFailed()) {
        <section class="status-card failed" aria-live="polite">
          <div class="status-content">
            <strong>{{ statusText() }}</strong>
            <p>{{ job()?.errorReason || 'Check the URL or backend settings and try again.' }}</p>
          </div>
          <button type="button" class="retry-button" (click)="job.set(null)">Try another video</button>
        </section>
      } @else if (!isFinished()) {
        <section class="status-card processing" aria-live="polite">
          <div class="spinner-row">
            <span class="spinner" aria-hidden="true"></span>
            <div>
              <strong>{{ statusText() }}</strong>
              <p>Processing video transcript and detecting ingredients…</p>
            </div>
          </div>

          <ol class="stages-stepper" role="progressbar" aria-valuemin="1" aria-valuemax="5"
            [attr.aria-valuenow]="statusStageIndex()">
            <li [class.active]="statusStageIndex() === 1" [class.done]="statusStageIndex() > 1">
              <span class="step-num">1</span> Pending
            </li>
            <li [class.active]="statusStageIndex() === 2" [class.done]="statusStageIndex() > 2">
              <span class="step-num">2</span> Transcribing
            </li>
            <li [class.active]="statusStageIndex() === 3" [class.done]="statusStageIndex() > 3">
              <span class="step-num">3</span> Detecting
            </li>
            <li [class.active]="statusStageIndex() === 4" [class.done]="statusStageIndex() > 4">
              <span class="step-num">4</span> Matching
            </li>
            <li [class.active]="statusStageIndex() === 5" [class.done]="statusStageIndex() > 5">
              <span class="step-num">5</span> Ready
            </li>
          </ol>
        </section>
      } @else if (job(); as result) {
        <div class="results-container">
          <!-- Video Metadata Panel -->
          <section class="panel metadata-panel">
            <div class="panel-header">
              <h2>Video Details</h2>
              @if (result.platform) {
                <span class="platform-badge">{{ result.platform }}</span>
              }
            </div>
            <div class="metadata-grid">
              <div class="meta-item">
                <span class="meta-label">Title</span>
                <span class="meta-value title">{{ result.title || 'Untitled Video' }}</span>
              </div>
              @if (result.author) {
                <div class="meta-item">
                  <span class="meta-label">Author</span>
                  <span class="meta-value">{{ result.author }}</span>
                </div>
              }
              @if (result.language) {
                <div class="meta-item">
                  <span class="meta-label">Language</span>
                  <span class="meta-value language-tag">{{ result.language.toUpperCase() }}</span>
                </div>
              }
              <div class="meta-item">
                <span class="meta-label">Source</span>
                <a [href]="result.sourceUrl" target="_blank" rel="noopener noreferrer" class="source-link">
                  View Original Video ↗
                </a>
              </div>
            </div>
          </section>

          <!-- Action Panel: Continue as Recipe -->
          <section class="panel continue-banner">
            <div>
              <h3>Ready to create a recipe?</h3>
              <p>Convert this video transcription and ingredient analysis into an editable recipe draft.</p>
            </div>
            <button type="button" class="primary-continue-btn" [disabled]="isContinuing()" (click)="continueAsRecipe()">
              {{ isContinuing() ? 'Converting…' : 'Continue as recipe →' }}
            </button>
          </section>

          <!-- Detected Ingredients Panel -->
          <section class="panel ingredients-panel">
            <div class="panel-header">
              <div>
                <h2>Detected Ingredients</h2>
                @if (ingredientsSummary(); as summary) {
                  <p class="summary-subtext">
                    {{ summary.total }} ingredients detected:
                    <span class="count-confident">{{ summary.confidentCount }} confident</span>,
                    <span class="count-uncertain">{{ summary.uncertainCount }} review needed</span>,
                    <span class="count-new">{{ summary.newCount }} new</span>
                  </p>
                }
              </div>
            </div>

            @if (!result.detectedIngredients.length) {
              <p class="empty-state">No ingredients were automatically detected in this transcript.</p>
            } @else {
              <div class="ingredients-list">
                @for (ing of result.detectedIngredients; track $index) {
                  <div class="ingredient-row" [class]="getConfidenceState(ing)">
                    <div class="ingredient-main">
                      <span class="ing-name">{{ ing.sourceName }}</span>
                      @if (ing.matchedIngredientName && ing.matchedIngredientName !== ing.sourceName) {
                        <small class="matched-sub">Matched to: {{ ing.matchedIngredientName }}</small>
                      }
                      @if (ing.note) {
                        <span class="ing-note">{{ ing.note }}</span>
                      }
                    </div>

                    <div class="ing-qty">
                      @if (ing.quantity) {
                        <span>{{ ing.quantity }}</span>
                      }
                      @if (ing.unit) {
                        <span>{{ ing.unit }}</span>
                      }
                    </div>

                    <div class="confidence-badge-cell">
                      @switch (getConfidenceState(ing)) {
                        @case ('confident') {
                          <span class="badge confident">✓ Confident</span>
                        }
                        @case ('uncertain') {
                          <span class="badge uncertain">⚠ Review Match</span>
                        }
                        @case ('new') {
                          <span class="badge new-ing">+ New Ingredient</span>
                        }
                      }
                    </div>
                  </div>
                }
              </div>
            }
          </section>

          <!-- Full Transcript Panel -->
          <section class="panel transcript-panel">
            <div class="panel-header">
              <h2>Full Transcript</h2>
              <button type="button" class="copy-btn" (click)="copyTranscript()">
                {{ isCopied() ? '✓ Copied!' : '📋 Copy transcript' }}
              </button>
            </div>
            <div class="transcript-content">
              {{ result.transcript || 'No transcript text available.' }}
            </div>
          </section>
        </div>
      }

      @if (error() && !hasFailed()) {
        <div class="error-banner" role="alert">{{ error() }}</div>
      }
    </main>
  `,
  styleUrl: './video-transcription-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VideoTranscriptionPage implements OnInit {
  private readonly toolsService = inject(ToolsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly url = signal('');
  readonly job = signal<VideoTranscriptionJob | null>(null);
  readonly isStarting = signal(false);
  readonly isContinuing = signal(false);
  readonly isCopied = signal(false);
  readonly error = signal<string | null>(null);

  readonly isValidUrl = computed(() => this.isSupportedVideoUrl(this.url()));
  readonly isFinished = computed(() => {
    const status = this.job()?.status;
    return status === VideoTranscriptionStatus.Ready || status === VideoTranscriptionStatus.Failed;
  });
  readonly hasFailed = computed(() => this.job()?.status === VideoTranscriptionStatus.Failed);

  readonly statusStageIndex = computed(() => {
    switch (this.job()?.status) {
      case VideoTranscriptionStatus.Pending: return 1;
      case VideoTranscriptionStatus.Transcribing: return 2;
      case VideoTranscriptionStatus.DetectingIngredients: return 3;
      case VideoTranscriptionStatus.MatchingIngredients: return 4;
      case VideoTranscriptionStatus.Ready: return 5;
      case VideoTranscriptionStatus.Failed: return -1;
      default: return 0;
    }
  });

  readonly statusText = computed(() => {
    switch (this.job()?.status) {
      case VideoTranscriptionStatus.Pending: return 'Initializing video transcription…';
      case VideoTranscriptionStatus.Transcribing: return 'Transcribing audio from video…';
      case VideoTranscriptionStatus.DetectingIngredients: return 'Detecting ingredients from transcript…';
      case VideoTranscriptionStatus.MatchingIngredients: return 'Matching ingredients with pantry catalog…';
      case VideoTranscriptionStatus.Ready: return 'Transcription complete';
      case VideoTranscriptionStatus.Failed: return 'Transcription failed';
      default: return '';
    }
  });

  readonly ingredientsSummary = computed(() => {
    const list = this.job()?.detectedIngredients ?? [];
    if (!list.length) return null;

    let confidentCount = 0;
    let uncertainCount = 0;
    let newCount = 0;

    for (const item of list) {
      const state = this.getConfidenceState(item);
      if (state === 'confident') confidentCount++;
      else if (state === 'uncertain') uncertainCount++;
      else newCount++;
    }

    return {
      total: list.length,
      confidentCount,
      uncertainCount,
      newCount,
    };
  });

  ngOnInit(): void {
    const jobId =
      this.route.snapshot.paramMap.get('id') ||
      this.route.snapshot.queryParamMap.get('id');

    if (jobId) {
      this.pollJob(jobId);
    }
  }

  start(): void {
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
          this.pollJob(id);
        },
        error: (err) => {
          this.error.set(getApiError(err, 'Could not start video transcription.'));
        },
      });
  }

  continueAsRecipe(): void {
    const currentJob = this.job();
    if (!currentJob || this.isContinuing()) return;

    if (currentJob.recipeImportJobId) {
      this.router.navigate(['/recipes/import', currentJob.recipeImportJobId]);
      return;
    }

    this.isContinuing.set(true);
    this.error.set(null);

    this.toolsService
      .continueAsRecipe(currentJob.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.isContinuing.set(false))
      )
      .subscribe({
        next: ({ id }) => {
          this.router.navigate(['/recipes/import', id]);
        },
        error: (err) => {
          this.error.set(getApiError(err, 'Could not create recipe import draft.'));
        },
      });
  }

  copyTranscript(): void {
    const text = this.job()?.transcript;
    if (!text) return;

    if (navigator.clipboard?.writeText) {
      navigator.clipboard.writeText(text).then(() => {
        this.isCopied.set(true);
        setTimeout(() => this.isCopied.set(false), 2000);
      });
    }
  }

  getConfidenceState(item: DetectedIngredient): IngredientConfidenceState {
    if (item.isProposedNew) {
      return 'new';
    }

    if (item.matchedIngredientId) {
      return (item.matchScore ?? 0) >= 0.8 ? 'confident' : 'uncertain';
    }

    return 'new';
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

  private pollJob(id: string): void {
    timer(0, 1000)
      .pipe(
        switchMap(() => this.toolsService.getVideoTranscription(id)),
        takeWhile(
          (job) =>
            job.status !== VideoTranscriptionStatus.Ready &&
            job.status !== VideoTranscriptionStatus.Failed,
          true
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (job) => {
          this.job.set(job);
          if (job.status === VideoTranscriptionStatus.Failed) {
            this.error.set(job.errorReason ?? 'Video transcription failed.');
          }
        },
        error: (err) => {
          this.error.set(getApiError(err, 'Could not check video transcription status.'));
        },
      });
  }
}
