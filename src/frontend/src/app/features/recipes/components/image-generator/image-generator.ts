import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, Subscription } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { RecipeImageCandidate } from '../../models/recipe.models';
import { buildRecipeImageSearchQuery, RecipeImageSearchService } from '../../services/recipe-image-search.service';

@Component({
  selector: 'app-image-generator',
  imports: [TranslatePipe],
  templateUrl: './image-generator.html',
  styleUrl: './image-generator.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImageGenerator {
  private readonly service = inject(RecipeImageSearchService);
  private readonly translations = inject(TranslationService);
  private readonly destroyRef = inject(DestroyRef);

  readonly title = input('');
  readonly ingredients = input<readonly string[]>([]);
  readonly isSaving = input(false);
  readonly imageSelected = output<RecipeImageCandidate>();
  readonly searching = signal(false);
  readonly hasSearched = signal(false);
  readonly error = signal<string | null>(null);
  readonly candidates = signal<RecipeImageCandidate[]>([]);
  readonly activeIndex = signal(0);
  readonly selectedId = signal<string | null>(null);
  readonly activeCandidate = computed(() => this.candidates()[this.activeIndex()] ?? null);
  readonly failedPreviewIds = signal(new Set<string>());

  private searchSubscription?: Subscription;

  constructor() {
    effect(() => {
      if (this.isSaving()) this.searchSubscription?.unsubscribe();
    });
  }

  generate(): void {
    if (this.searching() || this.isSaving()) return;

    const query = buildRecipeImageSearchQuery(this.title(), this.ingredients());
    if (!query) {
      this.error.set(this.translations.translate('recipes.imageSearchError'));
      return;
    }

    this.searchSubscription?.unsubscribe();
    this.searching.set(true);
    this.hasSearched.set(true);
    this.error.set(null);
    this.candidates.set([]);
    this.activeIndex.set(0);
    this.selectedId.set(null);
    this.failedPreviewIds.set(new Set());
    this.searchSubscription = this.service.search(query).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => {
        this.searching.set(false);
        this.searchSubscription = undefined;
      }),
    ).subscribe({
      next: candidates => this.candidates.set(candidates),
      error: err => this.error.set(getApiError(err, this.translations.translate('recipes.imageSearchError'))),
    });
  }

  previous(): void {
    const count = this.candidates().length;
    if (count > 1) this.activeIndex.update(index => (index + count - 1) % count);
  }

  next(): void {
    const count = this.candidates().length;
    if (count > 1) this.activeIndex.update(index => (index + 1) % count);
  }

  select(candidate: RecipeImageCandidate): void {
    this.selectedId.set(candidate.externalImageId);
    this.imageSelected.emit(candidate);
  }

  previewFailed(candidate: RecipeImageCandidate): void {
    this.failedPreviewIds.update(ids => new Set(ids).add(candidate.externalImageId));
  }

  hasPreview(candidate: RecipeImageCandidate): boolean {
    return !this.failedPreviewIds().has(candidate.externalImageId);
  }
}
