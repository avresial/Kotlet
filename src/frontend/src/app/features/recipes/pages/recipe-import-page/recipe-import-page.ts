import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize, switchMap, takeWhile, timer } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import {
  RecipeImportDraft,
  RecipeImportIngredient,
  RecipeImportJob,
  RecipeImportStatus,
} from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';

@Component({
  selector: 'app-recipe-import-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './recipe-import-page.html',
  styleUrl: './recipe-import-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeImportPage {
  private readonly service = inject(RecipeService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly url = signal('');
  readonly job = signal<RecipeImportJob | null>(null);
  readonly isStarting = signal(false);
  readonly isSaving = signal(false);
  readonly error = signal<string | null>(null);
  readonly draft = computed(() => this.job()?.draft ?? null);
  readonly hasFailed = computed(() => this.job()?.status === RecipeImportStatus.Failed);
  readonly isValidUrl = computed(() => this.isSupportedVideoUrl(this.url()));
  readonly statusText = computed(() => {
    switch (this.job()?.status) {
      case RecipeImportStatus.Pending: return 'Waiting to start…';
      case RecipeImportStatus.FetchingTranscript: return 'Fetching video transcript…';
      case RecipeImportStatus.Extracting: return 'Extracting recipe…';
      case RecipeImportStatus.ResolvingIngredients: return 'Matching ingredients…';
      case RecipeImportStatus.ReadyForReview: return 'Ready to review';
      case RecipeImportStatus.Failed: return 'Import failed';
      default: return '';
    }
  });

  start(): void {
    if (!this.isValidUrl() || this.isStarting()) return;
    this.isStarting.set(true);
    this.error.set(null);
    this.service.startImport(this.url().trim()).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isStarting.set(false)),
    ).subscribe({
      next: ({ id }) => this.poll(id),
      error: err => this.error.set(getApiError(err, 'Could not start the recipe import.')),
    });
  }

  save(): void {
    const job = this.job();
    const draft = job?.draft;
    if (!job || !draft || this.isSaving() || !this.isDraftValid(draft)) return;
    this.isSaving.set(true);
    this.error.set(null);
    this.service.acceptImport(job.id, draft).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isSaving.set(false)),
    ).subscribe({
      next: recipe => this.router.navigate(['/recipes', recipe.id], { state: { justCreated: true } }),
      error: err => this.error.set(getApiError(err, 'Could not save the imported recipe.')),
    });
  }

  updateDraft(change: Partial<RecipeImportDraft>): void {
    this.job.update(job => job?.draft ? { ...job, draft: { ...job.draft, ...change } } : job);
  }

  updateIngredient(index: number, change: Partial<RecipeImportIngredient>): void {
    const draft = this.draft();
    if (!draft) return;
    const ingredients = draft.ingredients.map((ingredient, i) => i === index ? { ...ingredient, ...change } : ingredient);
    this.updateDraft({ ingredients });
  }

  isDraftValid(draft: RecipeImportDraft): boolean {
    return !!draft.title.trim() && draft.servings >= 1 && draft.servings <= 99 &&
      draft.ingredients.length > 0 && draft.ingredients.every(x => !!x.name.trim() && (x.quantity ?? 0) > 0 && !!x.unit?.trim());
  }

  private poll(id: string): void {
    timer(0, 1000).pipe(
      switchMap(() => this.service.getImport(id)),
      takeWhile(job => job.status !== RecipeImportStatus.ReadyForReview && job.status !== RecipeImportStatus.Failed, true),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: job => {
        this.job.set(job);
        if (job.status === RecipeImportStatus.Failed) this.error.set(job.errorReason ?? 'Recipe import failed.');
      },
      error: err => this.error.set(getApiError(err, 'Could not check recipe import progress.')),
    });
  }

  private isSupportedVideoUrl(value: string): boolean {
    try {
      const url = new URL(value.trim());
      return url.protocol === 'http:' || url.protocol === 'https:'
        ? /(^|\.)(youtube\.com|youtu\.be|tiktok\.com)$/i.test(url.hostname)
        : false;
    } catch { return false; }
  }
}
