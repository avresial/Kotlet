import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import {
  RecipeImportDraft,
  RecipeImportIngredient,
  RecipeImportJob,
  RecipeImportStatus,
} from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';

@Component({
  selector: 'app-recipe-import-page',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="import-page">
      <a class="back" routerLink="/tools">← Back to Tools</a>
      <header>
        <p class="eyebrow">Recipe Review</p>
        <h1>Review imported recipe</h1>
        <p>Review and edit the extracted recipe draft before saving it to your collection.</p>
      </header>

      @if (!jobId()) {
        <section class="start-card" aria-live="polite">
          <div>
            <h2>No import job selected</h2>
            <p>Recipe imports are started from the Tools hub after transcribing a video or running an import job.</p>
          </div>
          <a routerLink="/tools" class="tools-btn">Go to Tools Hub →</a>
        </section>
      } @else if (isLoading()) {
        <section class="progress-card" aria-live="polite">
          <span class="spinner" aria-hidden="true"></span>
          <div><strong>Loading recipe import…</strong><p>Fetching extracted draft data.</p></div>
        </section>
      } @else if (hasFailed()) {
        <section class="progress-card failed" aria-live="polite">
          <div><strong>{{ statusText() }}</strong><p>{{ error() || 'Check the import job and try again.' }}</p></div>
        </section>
      } @else if (draft(); as value) {
        <form class="review" (ngSubmit)="save()">
          <div class="review-heading">
            <div><p class="eyebrow">Review draft</p><h2>{{ value.title || 'Untitled Recipe' }}</h2></div>
            <span class="ai-badge">AI-assisted</span>
          </div>
          @if (value.gaps.length) {
            <div class="gaps"><strong>Please check:</strong><ul>@for (gap of value.gaps; track gap) { <li>{{ gap }}</li> }</ul></div>
          }
          @if (value.duplicateMatches.length) {
            <div class="gaps"><strong>Possible duplicate:</strong><ul>@for (match of value.duplicateMatches; track match.recipeId) {
              <li><a [routerLink]="['/recipes', match.recipeId]">{{ match.title }}</a></li>
            }</ul></div>
          }
          <label>Title <input name="title" [ngModel]="value.title" (ngModelChange)="updateDraft({ title: $event })" required maxlength="160" /></label>
          <label class="servings">Servings <input type="number" name="servings" [ngModel]="value.servings" (ngModelChange)="updateDraft({ servings: $event })" min="1" max="99" required /></label>
          <label>Instructions <textarea name="instructions" [ngModel]="value.instructionsMarkdown" (ngModelChange)="updateDraft({ instructionsMarkdown: $event })" rows="10"></textarea></label>

          <fieldset><legend>Ingredients</legend>
            @for (ingredient of value.ingredients; track $index; let i = $index) {
              <div class="ingredient" [class.proposed]="ingredient.isProposedNew">
                <div class="ingredient-name">
                  <input [name]="'name-' + i" [ngModel]="ingredient.name" (ngModelChange)="updateIngredientName(i, $event)" list="ingredient-catalog" required />
                  @if (ingredient.isProposedNew) { <span>New ingredient</span> }
                  @else { <small>Matched to {{ ingredient.matchedName }}</small> }
                </div>
                <input type="number" [name]="'quantity-' + i" [ngModel]="ingredient.quantity" (ngModelChange)="updateIngredient(i, { quantity: $event })" min="0.001" step="any" aria-label="Quantity" required />
                <input [name]="'unit-' + i" [ngModel]="ingredient.unit" (ngModelChange)="updateIngredient(i, { unit: $event })" aria-label="Unit" placeholder="unit" required />
                <input [name]="'note-' + i" [ngModel]="ingredient.note" (ngModelChange)="updateIngredient(i, { note: $event })" aria-label="Note" placeholder="note" />
              </div>
            }
          </fieldset>
          <datalist id="ingredient-catalog">@for (item of catalog(); track item.id) { <option [value]="item.name"></option> }</datalist>
          <div class="actions"><a routerLink="/recipes">Cancel</a><button type="submit" [disabled]="isSaving() || !isDraftValid(value)">{{ isSaving() ? 'Saving…' : 'Save recipe' }}</button></div>
        </form>
      }

      @if (error() && !hasFailed()) { <div class="error" role="alert">{{ error() }}</div> }
    </main>
  `,
  styleUrl: './recipe-import-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeImportPage implements OnInit {
  private readonly service = inject(RecipeService);
  private readonly ingredientService = inject(IngredientService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly jobId = signal<string | null>(null);
  readonly job = signal<RecipeImportJob | null>(null);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly error = signal<string | null>(null);
  readonly catalog = signal<Ingredient[]>([]);

  readonly draft = computed(() => this.job()?.draft ?? null);
  readonly hasFailed = computed(() => this.job()?.status === RecipeImportStatus.Failed);
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

  constructor() {
    this.ingredientService.getAll().pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: ingredients => this.catalog.set(ingredients), error: () => this.catalog.set([]) });
  }

  ngOnInit(): void {
    const id =
      this.route.snapshot.paramMap.get('jobId') ||
      this.route.snapshot.paramMap.get('id') ||
      this.route.snapshot.queryParamMap.get('jobId') ||
      this.route.snapshot.queryParamMap.get('id');

    if (id) {
      this.jobId.set(id);
      this.loadJob(id);
    }
  }

  loadJob(id: string): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.service.getImport(id).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isLoading.set(false))
    ).subscribe({
      next: job => {
        this.job.set(job);
        if (job.status === RecipeImportStatus.Failed) {
          this.error.set(job.errorReason ?? 'Recipe import failed.');
        }
      },
      error: err => this.error.set(getApiError(err, 'Could not load the recipe import draft.')),
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

  updateIngredientName(index: number, name: string): void {
    const match = this.catalog().find(x => x.name.localeCompare(name, undefined, { sensitivity: 'accent' }) === 0);
    this.updateIngredient(index, {
      name,
      ingredientId: match?.id ?? null,
      matchedName: match?.name ?? null,
      matchScore: match ? 1 : null,
      isProposedNew: !match,
    });
  }

  isDraftValid(draft: RecipeImportDraft): boolean {
    return !!draft.title.trim() && draft.servings >= 1 && draft.servings <= 99 &&
      draft.ingredients.length > 0 && draft.ingredients.every(x => !!x.name.trim() && (x.quantity ?? 0) > 0 && !!x.unit?.trim());
  }
}
