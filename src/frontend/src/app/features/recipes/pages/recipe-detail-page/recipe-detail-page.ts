import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { finalize, forkJoin } from 'rxjs';
import DOMPurify from 'dompurify';
import { marked } from 'marked';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { RecipeDetail, RecipeIngredient } from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';
import { ImageGallery } from '../../components/image-gallery/image-gallery';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { recipeCaloriesPerServing, recipePricePerServing } from '../../../meal-planner/meal-planner-calculations';
import { AiBadge } from '../../../../shared/ui/ai-badge/ai-badge';

@Component({
  selector: 'app-recipe-detail-page',
  imports: [RouterLink, DatePipe, ImageGallery, TranslatePipe, AiBadge],
  templateUrl: './recipe-detail-page.html',
  styleUrl: './recipe-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeDetailPage implements OnInit {
  private readonly service = inject(RecipeService);
  private readonly ingredientService = inject(IngredientService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly translations = inject(TranslationService);

  readonly recipe = signal<RecipeDetail | null>(null);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly isLoading = signal(true);
  readonly isDeleting = signal(false);
  readonly error = signal<string | null>(null);
  readonly justCreated = signal(!!this.router.getCurrentNavigation()?.extras.state?.['justCreated']);
  readonly pricePerServing = computed(() => this.recipe() ? recipePricePerServing(this.recipe()!, this.ingredients()) : 0);
  readonly caloriesPerServing = computed(() => this.recipe() ? recipeCaloriesPerServing(this.recipe()!, this.ingredients()) : 0);

  readonly descriptionHtml = computed<SafeHtml>(() => {
    const md = this.recipe()?.descriptionMarkdown;
    if (!md?.trim()) return '';
    const raw = marked.parse(md, { async: false }) as string;
    return this.sanitizer.bypassSecurityTrustHtml(DOMPurify.sanitize(raw));
  });

  get id(): string { return this.route.snapshot.paramMap.get('id')!; }

  displayQuantity(ingredient: RecipeIngredient): number {
    return Number.isInteger(ingredient.quantity) ? ingredient.quantity : ingredient.normalizedQuantity;
  }

  displayUnit(ingredient: RecipeIngredient): string {
    return Number.isInteger(ingredient.quantity) ? ingredient.unit : ingredient.normalizedUnit;
  }

  hasConvertedMeasurement(ingredient: RecipeIngredient): boolean {
    return this.displayQuantity(ingredient) !== ingredient.normalizedQuantity
      || this.displayUnit(ingredient) !== ingredient.normalizedUnit;
  }

  ngOnInit(): void {
    forkJoin({ recipe: this.service.get(this.id), ingredients: this.ingredientService.getAll() })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ recipe, ingredients }) => { this.recipe.set(recipe); this.ingredients.set(ingredients); },
        error: (err) => this.error.set(getApiError(err, this.translations.translate('recipes.loadOneError'))),
      });
  }

  addNext(): void {
    this.router.navigate(['/recipes/new']);
  }

  delete(): void {
    if (!window.confirm(this.translations.translate('recipes.deleteConfirm').replace('{name}', this.recipe()?.title ?? ''))) return;
    this.isDeleting.set(true);
    this.service.delete(this.id).subscribe({
      next: () => this.router.navigate(['/recipes']),
      error: (err) => {
        this.error.set(getApiError(err, this.translations.translate('recipes.deleteError')));
        this.isDeleting.set(false);
      },
    });
  }
}
