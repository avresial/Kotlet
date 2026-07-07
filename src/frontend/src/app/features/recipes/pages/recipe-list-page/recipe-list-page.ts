import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize, Subscription } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { IngredientPicker } from '../../../ingredients/components/ingredient-picker/ingredient-picker';
import { RecipeMealType, RecipeSummary, recipeMealTypes } from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';

@Component({
  selector: 'app-recipe-list-page',
  imports: [RouterLink, FormsModule, DatePipe, TranslatePipe, IngredientPicker],
  templateUrl: './recipe-list-page.html',
  styleUrl: './recipe-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeListPage implements OnInit {
  private readonly service = inject(RecipeService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly translations = inject(TranslationService);
  private readonly ingredientService = inject(IngredientService);

  readonly recipes = signal<RecipeSummary[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly deletingId = signal<string | null>(null);
  readonly search = signal('');
  readonly mealType = signal<RecipeMealType | ''>('');
  readonly ingredients = signal<Ingredient[]>([]);
  readonly selectedIngredientIds = signal<string[]>([]);
  readonly ingredientPickerValue = signal('');
  readonly selectedIngredients = computed(() => this.selectedIngredientIds()
    .map(id => this.ingredients().find(ingredient => ingredient.id === id))
    .filter((ingredient): ingredient is Ingredient => ingredient !== undefined));
  readonly availableIngredients = computed(() => this.ingredients()
    .filter(ingredient => !this.selectedIngredientIds().includes(ingredient.id)));
  readonly mealTypes = recipeMealTypes;
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly imageUrls = signal<Record<string, string>>({});
  readonly pageSize = 20;
  private loadSub?: Subscription;
  private imageSubs = new Subscription();

  constructor() {
    this.destroyRef.onDestroy(() => this.clearImageUrls());
  }

  ngOnInit(): void {
    this.load();
    this.ingredientService.getAll().subscribe({
      next: ingredients => this.ingredients.set(ingredients),
      error: err => this.error.set(getApiError(err, this.translations.translate('ingredients.loadError'))),
    });
  }

  load(): void {
    this.loadSub?.unsubscribe();
    this.isLoading.set(true);
    this.error.set(null);
    this.loadSub = this.service
      .list(this.page(), this.pageSize, this.search() || undefined, this.mealType() || undefined, this.selectedIngredientIds())
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (res) => {
          this.clearImageUrls();
          this.recipes.set(res.items);
          this.totalCount.set(res.totalCount);
          this.loadImages(res.items);
        },
        error: (err) => this.error.set(getApiError(err, this.translations.translate('recipes.loadError'))),
      });
  }

  onSearch(): void {
    this.page.set(1);
    this.load();
  }

  clearSearch(): void {
    this.search.set('');
    this.mealType.set('');
    this.selectedIngredientIds.set([]);
    this.ingredientPickerValue.set('');
    this.onSearch();
  }

  addIngredient(ingredient: Ingredient): void {
    if (this.selectedIngredientIds().length >= 100 || this.selectedIngredientIds().includes(ingredient.id)) return;
    this.selectedIngredientIds.update(ids => [...ids, ingredient.id]);
    this.ingredientPickerValue.set('');
    this.onSearch();
  }

  removeIngredient(ingredientId: string): void {
    if (!this.selectedIngredientIds().includes(ingredientId)) return;
    this.selectedIngredientIds.update(ids => ids.filter(id => id !== ingredientId));
    this.onSearch();
  }

  prevPage(): void {
    if (this.page() <= 1) return;
    this.page.update((p) => p - 1);
    this.load();
  }

  nextPage(): void {
    if (this.page() * this.pageSize >= this.totalCount()) return;
    this.page.update((p) => p + 1);
    this.load();
  }

  delete(recipe: RecipeSummary): void {
    if (this.deletingId() || !window.confirm(this.translations.translate('recipes.deleteConfirm').replace('{name}', recipe.title))) return;
    this.deletingId.set(recipe.id);
    this.service.delete(recipe.id).pipe(finalize(() => this.deletingId.set(null))).subscribe({
      next: () => {
        this.recipes.update((items) => items.filter((r) => r.id !== recipe.id));
        this.totalCount.update((n) => n - 1);
      },
      error: (err) => this.error.set(getApiError(err, this.translations.translate('recipes.deleteError'))),
    });
  }

  navigateTo(recipe: RecipeSummary): void {
    this.router.navigate(['/recipes', recipe.id]);
  }

  get hasNextPage(): boolean {
    return this.page() * this.pageSize < this.totalCount();
  }

  private loadImages(recipes: RecipeSummary[]): void {
    for (const recipe of recipes) {
      if (!recipe.firstImageUrl) continue;

      const imageSub = this.service.imageContent(recipe.firstImageUrl as `/api/${string}`)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (blob) => {
            const url = URL.createObjectURL(blob);
            this.imageUrls.update((urls) => ({ ...urls, [recipe.id]: url }));
          },
        });
      this.imageSubs.add(imageSub);
    }
  }

  private clearImageUrls(): void {
    this.imageSubs.unsubscribe();
    this.imageSubs = new Subscription();
    for (const url of Object.values(this.imageUrls())) URL.revokeObjectURL(url);
    this.imageUrls.set({});
  }
}
