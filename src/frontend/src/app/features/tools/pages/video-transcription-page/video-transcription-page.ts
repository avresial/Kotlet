import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, switchMap, takeWhile, timer } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  DetectedIngredient,
  IngredientConfidenceState,
  VideoTranscriptionJob,
  VideoTranscriptionStatus,
} from '../../models/tools.models';
import { ToolsService } from '../../services/tools.service';

@Component({
  selector: 'app-video-transcription-page',
  imports: [FormsModule, RouterLink, TranslatePipe],
  template: `
    <main class="transcription-page">
      <a class="back-link" routerLink="/tools">{{ 'tools.video.backToTools' | t }}</a>

      <header>
        <div class="title-row">
          <span class="ai-badge">{{ 'tools.video.aiAssisted' | t }}</span>
          <h1>{{ 'tools.video.title' | t }}</h1>
        </div>
        <p>{{ 'tools.video.intro' | t }}</p>
      </header>

      @if (!job()) {
        <form class="start-card" (ngSubmit)="start()">
          <label for="video-url">{{ 'tools.video.videoUrl' | t }}</label>
          <div class="url-row">
            <input
              id="video-url"
              type="url"
              name="url"
              [ngModel]="url()"
              (ngModelChange)="url.set($event)"
              [placeholder]="'tools.video.urlPlaceholder' | t"
              autocomplete="url"
              required
            />
            <button type="submit" [disabled]="!isValidUrl() || isStarting()">
              {{ (isStarting() ? 'tools.video.starting' : 'tools.video.transcribe') | t }}
            </button>
          </div>
          @if (url() && !isValidUrl()) {
            <p class="field-error">{{ 'tools.video.invalidUrl' | t }}</p>
          }
        </form>
      } @else if (hasFailed()) {
        <section class="status-card failed" aria-live="polite">
          <div class="status-content">
            <strong>{{ statusText() }}</strong>
            <p>{{ job()?.errorReason || ('tools.video.failureFallback' | t) }}</p>
          </div>
          <button type="button" class="retry-button" (click)="job.set(null)">{{ 'tools.video.retry' | t }}</button>
        </section>
      } @else if (!isFinished()) {
        <section class="status-card processing" aria-live="polite">
          <div class="spinner-row">
            <span class="spinner" aria-hidden="true"></span>
            <div>
              <strong>{{ statusText() }}</strong>
              <p>{{ 'tools.video.processing' | t }}</p>
            </div>
          </div>

          <ol class="stages-stepper" role="progressbar" aria-valuemin="1" aria-valuemax="5"
            [attr.aria-valuenow]="statusStageIndex()">
            <li [class.active]="statusStageIndex() === 1" [class.done]="statusStageIndex() > 1">
              <span class="step-num">1</span> {{ 'tools.video.stage.pending' | t }}
            </li>
            <li [class.active]="statusStageIndex() === 2" [class.done]="statusStageIndex() > 2">
              <span class="step-num">2</span> {{ 'tools.video.stage.transcribing' | t }}
            </li>
            <li [class.active]="statusStageIndex() === 3" [class.done]="statusStageIndex() > 3">
              <span class="step-num">3</span> {{ 'tools.video.stage.detecting' | t }}
            </li>
            <li [class.active]="statusStageIndex() === 4" [class.done]="statusStageIndex() > 4">
              <span class="step-num">4</span> {{ 'tools.video.stage.matching' | t }}
            </li>
            <li [class.active]="statusStageIndex() === 5" [class.done]="statusStageIndex() > 5">
              <span class="step-num">5</span> {{ 'tools.video.stage.ready' | t }}
            </li>
          </ol>
        </section>
      } @else if (job(); as result) {
        <div class="results-container">
          <!-- Video Metadata Panel -->
          <section class="panel metadata-panel">
            <div class="panel-header">
              <h2>{{ 'tools.video.videoDetails' | t }}</h2>
              @if (result.platform) {
                <span class="platform-badge">{{ result.platform }}</span>
              }
            </div>
            <div class="metadata-grid">
              <div class="meta-item">
                <span class="meta-label">{{ 'tools.video.titleLabel' | t }}</span>
                <span class="meta-value title">{{ result.title || ('tools.video.untitledVideo' | t) }}</span>
              </div>
              @if (result.author) {
                <div class="meta-item">
                  <span class="meta-label">{{ 'tools.video.author' | t }}</span>
                  <span class="meta-value">{{ result.author }}</span>
                </div>
              }
              @if (result.language) {
                <div class="meta-item">
                  <span class="meta-label">{{ 'tools.video.language' | t }}</span>
                  <span class="meta-value language-tag">{{ result.language.toUpperCase() }}</span>
                </div>
              }
              <div class="meta-item">
                <span class="meta-label">{{ 'tools.video.source' | t }}</span>
                <a [href]="result.sourceUrl" target="_blank" rel="noopener noreferrer" class="source-link">
                  {{ 'tools.video.viewOriginal' | t }}
                </a>
              </div>
            </div>
          </section>

          <!-- Action Panel: Continue as Recipe -->
          <section class="panel continue-banner">
            <div>
              <h3>{{ 'tools.video.readyToCreateRecipe' | t }}</h3>
              <p>{{ 'tools.video.continueDescription' | t }}</p>
            </div>
            <button type="button" class="primary-continue-btn" [disabled]="isContinuing()" (click)="continueAsRecipe()">
              {{ (isContinuing() ? 'tools.video.converting' : 'tools.video.continueAsRecipe') | t }}
            </button>
          </section>

          <!-- Detected Ingredients Panel -->
          <section class="panel ingredients-panel">
            <div class="panel-header">
              <div>
                <h2>{{ 'tools.video.detectedIngredients' | t }}</h2>
                @if (ingredientsSummary(); as summary) {
                  <p class="summary-subtext">
                    {{ summary.total }} {{ 'tools.video.ingredientsDetected' | t }}:
                    <span class="count-confident">{{ summary.confidentCount }} {{ 'tools.video.confident' | t }}</span>,
                    <span class="count-uncertain">{{ summary.uncertainCount }} {{ 'tools.video.reviewNeeded' | t }}</span>,
                    <span class="count-new">{{ summary.newCount }} {{ 'tools.video.new' | t }}</span>
                  </p>
                }
              </div>
            </div>

            @if (!result.detectedIngredients.length) {
              <p class="empty-state">{{ 'tools.video.emptyIngredients' | t }}</p>
            } @else {
              <div class="ingredients-list">
                @for (ing of result.detectedIngredients; track $index) {
                  <div class="ingredient-row" [class]="getConfidenceState(ing)">
                    <div class="ingredient-main">
                      <span class="ing-name">{{ ing.sourceName }}</span>
                      @if (ing.matchedIngredientName && ing.matchedIngredientName !== ing.sourceName) {
                        <small class="matched-sub">{{ 'tools.video.matchedTo' | t }} {{ ing.matchedIngredientName }}</small>
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
                          <span class="badge confident">{{ 'tools.video.badgeConfident' | t }}</span>
                        }
                        @case ('uncertain') {
                          <span class="badge uncertain">{{ 'tools.video.badgeReviewMatch' | t }}</span>
                        }
                        @case ('new') {
                          <span class="badge new-ing">{{ 'tools.video.badgeNewIngredient' | t }}</span>
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
              <h2>{{ 'tools.video.fullTranscript' | t }}</h2>
              <button type="button" class="copy-btn" (click)="copyTranscript()">
                {{ (isCopied() ? 'tools.video.copied' : 'tools.video.copyTranscript') | t }}
              </button>
            </div>
            <div class="transcript-content">
              {{ result.transcript || ('tools.video.noTranscript' | t) }}
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
  private readonly translations = inject(TranslationService);

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
      case VideoTranscriptionStatus.Pending: return this.translations.translate('tools.video.status.pending');
      case VideoTranscriptionStatus.Transcribing: return this.translations.translate('tools.video.status.transcribing');
      case VideoTranscriptionStatus.DetectingIngredients: return this.translations.translate('tools.video.status.detecting');
      case VideoTranscriptionStatus.MatchingIngredients: return this.translations.translate('tools.video.status.matching');
      case VideoTranscriptionStatus.Ready: return this.translations.translate('tools.video.status.ready');
      case VideoTranscriptionStatus.Failed: return this.translations.translate('tools.video.status.failed');
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
          this.error.set(getApiError(err, this.translations.translate('tools.video.startError')));
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
          this.error.set(getApiError(err, this.translations.translate('tools.video.continueError')));
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
            this.error.set(job.errorReason ?? this.translations.translate('tools.video.transcriptionFailed'));
          }
        },
        error: (err) => {
          this.error.set(getApiError(err, this.translations.translate('tools.video.statusError')));
        },
      });
  }
}
