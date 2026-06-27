import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { PantryItem } from '../../../pantry/pantry.models';
import { PantryService } from '../../../pantry/pantry.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { ShoppingListItem } from '../../../shopping-list/shopping-list.models';
import { ShoppingListService } from '../../../shopping-list/shopping-list.service';
import { getApiError } from '../../../../core/http/api-error';
import { RecipeSummary } from '../../../recipes/models/recipe.models';
import { RecipeService } from '../../../recipes/services/recipe.service';

@Component({
  selector: 'app-home-page',
  imports: [RouterLink, FormsModule],
  templateUrl: './home-page.html',
  styleUrl: './home-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage implements OnInit {
  readonly auth = inject(AuthService);
  private readonly pantryService = inject(PantryService);
  private readonly ingredientService = inject(IngredientService);
  private readonly shoppingListService = inject(ShoppingListService);
  private readonly recipeService = inject(RecipeService);
  readonly lowStock = signal<PantryItem[]>([]);
  readonly pantryLoading = signal(true);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly shoppingItems = signal<ShoppingListItem[]>([]);
  readonly shoppingLoading = signal(true);
  readonly shoppingSaving = signal(false);
  readonly shoppingError = signal<string | null>(null);
  readonly newestRecipes = signal<RecipeSummary[]>([]);
  readonly recipesLoading = signal(true);
  readonly recipesError = signal(false);
  readonly selectedIngredientId = signal('');
  readonly newQuantity = signal(1);
  readonly availableIngredients = computed(() => this.ingredients().filter(ingredient =>
    !this.shoppingItems().some(item => item.ingredientId === ingredient.id)));
  readonly purchasedCount = computed(() => this.shoppingItems().filter(item => item.isPurchased).length);
  readonly totalPrice = computed(() => this.shoppingItems().reduce((total, item) => total + item.totalPrice, 0));
  readonly shoppingProgress = computed(() => this.shoppingItems().length
    ? Math.round(this.purchasedCount() / this.shoppingItems().length * 100) : 0);
  readonly firstName = computed(() => {
    const user = this.auth.currentUser();
    return user?.displayName?.trim().split(/\s+/)[0] || user?.email.split('@')[0] || 'there';
  });
  readonly today = new Intl.DateTimeFormat('en', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
  }).format(new Date());

  readonly todaysMenu = [
    { time: 'BREAKFAST', emoji: '🍳', name: 'Jajecznica', note: 'Scrambled eggs with chives' },
    { time: 'LUNCH', emoji: '🥪', name: 'Kanapki', note: 'Open sandwiches on rye' },
    { time: 'DINNER', emoji: '🍽️', name: 'Kotlet schabowy', note: 'A Polish classic for tonight’s table' },
  ];

  ngOnInit(): void {
    this.recipeService.listRecent(4).pipe(finalize(() => this.recipesLoading.set(false))).subscribe({
      next: recipes => this.newestRecipes.set(recipes),
      error: () => this.recipesError.set(true),
    });
    forkJoin({ pantry: this.pantryService.getAll(), ingredients: this.ingredientService.getAll(), shopping: this.shoppingListService.getAll() })
      .pipe(finalize(() => { this.pantryLoading.set(false); this.shoppingLoading.set(false); }))
      .subscribe({
        next: ({ pantry, ingredients, shopping }) => {
          this.lowStock.set(pantry.slice(0, 5));
          this.ingredients.set(ingredients);
          this.shoppingItems.set(shopping);
        },
        error: error => this.shoppingError.set(getApiError(error, 'Unable to load the dashboard.')),
      });
  }

  relativeDate(value: string): string {
    const elapsedDays = Math.max(0, Math.floor((Date.now() - new Date(value).getTime()) / 86_400_000));
    if (elapsedDays === 0) return 'today';
    if (elapsedDays === 1) return 'yesterday';
    if (elapsedDays < 7) return `${elapsedDays} days ago`;
    const weeks = Math.floor(elapsedDays / 7);
    if (weeks < 5) return `${weeks} week${weeks === 1 ? '' : 's'} ago`;
    return new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric' }).format(new Date(value));
  }

  addToShoppingList(ingredientId = this.selectedIngredientId(), quantity = this.newQuantity()): void {
    if (!ingredientId || !Number.isFinite(quantity) || quantity <= 0 || this.shoppingSaving()) return;
    this.shoppingSaving.set(true); this.shoppingError.set(null);
    this.shoppingListService.create(ingredientId, quantity).pipe(finalize(() => this.shoppingSaving.set(false))).subscribe({
      next: item => {
        this.shoppingItems.update(items => [...items, item]);
        this.selectedIngredientId.set(''); this.newQuantity.set(1);
      },
      error: error => this.shoppingError.set(getApiError(error, 'Unable to add this ingredient.')),
    });
  }

  updateItem(item: ShoppingListItem, changes: Partial<Pick<ShoppingListItem, 'quantity' | 'isPurchased'>>): void {
    if (changes.quantity !== undefined && (!Number.isFinite(changes.quantity) || changes.quantity <= 0)) return;
    this.shoppingSaving.set(true); this.shoppingError.set(null);
    this.shoppingListService.update(item, changes).pipe(finalize(() => this.shoppingSaving.set(false))).subscribe({
      next: updated => this.shoppingItems.update(items => items.map(current => current.id === updated.id ? updated : current)),
      error: error => this.shoppingError.set(getApiError(error, 'Unable to update this item.')),
    });
  }

  removeItem(item: ShoppingListItem): void {
    this.shoppingSaving.set(true); this.shoppingError.set(null);
    this.shoppingListService.delete(item.id).pipe(finalize(() => this.shoppingSaving.set(false))).subscribe({
      next: () => this.shoppingItems.update(items => items.filter(current => current.id !== item.id)),
      error: error => this.shoppingError.set(getApiError(error, 'Unable to remove this item.')),
    });
  }

  clearChecked(): void {
    if (this.shoppingSaving() || this.purchasedCount() === 0) return;
    this.shoppingSaving.set(true); this.shoppingError.set(null);
    this.shoppingListService.clearChecked().pipe(finalize(() => this.shoppingSaving.set(false))).subscribe({
      next: () => this.shoppingItems.update(items => items.filter(item => !item.isPurchased)),
      error: error => this.shoppingError.set(getApiError(error, 'Unable to clear checked items.')),
    });
  }

  isOnShoppingList(ingredientId: string): boolean {
    return this.shoppingItems().some(item => item.ingredientId === ingredientId);
  }
}
