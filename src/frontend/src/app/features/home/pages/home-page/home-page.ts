import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
import { DailyMealPlan, MealParticipant, MealSlot } from '../../../meal-planner/models/meal-planner.models';
import { MealPlannerService } from '../../../meal-planner/services/meal-planner.service';
import { HouseMember } from '../../home.models';
import { HomeService } from '../../home.service';

interface TodaysMenuEntry {
  id: string;
  recipeId: string | null;
  time: string;
  emoji: string;
  name: string;
  note: string | null;
  participants: MealParticipant[];
}

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
  private readonly mealPlannerService = inject(MealPlannerService);
  private readonly homeService = inject(HomeService);
  private readonly destroyRef = inject(DestroyRef);
  readonly lowStock = signal<PantryItem[]>([]);
  readonly pantryLoading = signal(true);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly shoppingItems = signal<ShoppingListItem[]>([]);
  readonly shoppingLoading = signal(true);
  readonly shoppingSaving = signal(false);
  readonly shoppingError = signal<string | null>(null);
  readonly newestRecipes = signal<RecipeSummary[]>([]);
  readonly recipeAvatarUrls = signal<Record<string, string>>({});
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

  readonly todaysMenu = signal<TodaysMenuEntry[]>([]);
  readonly menuAvatarUrls = signal<Record<string, string>>({});
  readonly menuLoading = signal(true);
  readonly menuError = signal(false);

  readonly houseName = signal<string | null>(null);
  readonly houseMembers = signal<HouseMember[]>([]);
  readonly houseLoading = signal(true);
  readonly houseError = signal(false);

  private readonly slotMeta: Record<MealSlot, { time: string; emoji: string }> = {
    breakfast: { time: 'BREAKFAST', emoji: '🍳' },
    dinner: { time: 'LUNCH', emoji: '🥪' },
    supper: { time: 'DINNER', emoji: '🍽️' },
  };
  private readonly slotOrder: MealSlot[] = ['breakfast', 'dinner', 'supper'];

  ngOnInit(): void {
    this.destroyRef.onDestroy(() => {
      Object.values(this.recipeAvatarUrls()).forEach(url => URL.revokeObjectURL(url));
      Object.values(this.menuAvatarUrls()).forEach(url => URL.revokeObjectURL(url));
    });
    this.recipeService.listRecent(4).pipe(finalize(() => this.recipesLoading.set(false))).subscribe({
      next: recipes => {
        this.newestRecipes.set(recipes);
        this.loadAvatars(recipes);
      },
      error: () => this.recipesError.set(true),
    });
    this.mealPlannerService.getForDate(this.todayString()).pipe(finalize(() => this.menuLoading.set(false))).subscribe({
      next: plan => {
        const menu = this.buildMenu(plan);
        this.todaysMenu.set(menu);
        this.loadMenuAvatars(menu);
      },
      error: () => this.menuError.set(true),
    });
    this.homeService.getHouse().pipe(finalize(() => this.houseLoading.set(false))).subscribe({
      next: house => { this.houseName.set(house.name); this.houseMembers.set(house.members); },
      error: () => this.houseError.set(true),
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

  relativeTime(dateStr: string): string {
    const diff = Date.now() - new Date(dateStr).getTime();
    const seconds = Math.floor(diff / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    const weeks = Math.floor(days / 7);
    const months = Math.floor(days / 30);
    const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
    if (months >= 1) return rtf.format(-months, 'month');
    if (weeks >= 1) return rtf.format(-weeks, 'week');
    if (days >= 1) return rtf.format(-days, 'day');
    if (hours >= 1) return rtf.format(-hours, 'hour');
    if (minutes >= 1) return rtf.format(-minutes, 'minute');
    return 'just now';
  }

  private loadAvatars(recipes: RecipeSummary[]): void {
    for (const recipe of recipes) {
      if (!recipe.firstImageUrl) continue;
      this.recipeService.imageContent(recipe.firstImageUrl as `/api/${string}`)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(blob => {
          const url = URL.createObjectURL(blob);
          this.recipeAvatarUrls.update(urls => ({ ...urls, [recipe.id]: url }));
        });
    }
  }

  private loadMenuAvatars(entries: TodaysMenuEntry[]): void {
    const recipeIds = [...new Set(entries.map(entry => entry.recipeId).filter((id): id is string => !!id))];
    for (const recipeId of recipeIds) {
      this.recipeService.listImages(recipeId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(images => {
          const first = images[0];
          if (!first) return;
          this.recipeService.imageContent(first.contentUrl)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe(blob => {
              const url = URL.createObjectURL(blob);
              this.menuAvatarUrls.update(urls => ({ ...urls, [recipeId]: url }));
            });
        });
    }
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

  memberName(member: HouseMember): string {
    return member.displayName?.trim() || member.email.split('@')[0];
  }

  memberInitials(member: HouseMember): string {
    const name = this.memberName(member);
    const parts = name.split(/\s+/).filter(Boolean);
    const initials = parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : name.slice(0, 2);
    return initials.toUpperCase();
  }

  participantInitials(displayName: string): string {
    const name = displayName.trim();
    const parts = name.split(/\s+/).filter(Boolean);
    const initials = parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : name.slice(0, 2);
    return initials.toUpperCase();
  }

  isOnShoppingList(ingredientId: string): boolean {
    return this.shoppingItems().some(item => item.ingredientId === ingredientId);
  }

  private buildMenu(plan: DailyMealPlan): TodaysMenuEntry[] {
    const entries: TodaysMenuEntry[] = [];
    for (const slot of this.slotOrder) {
      const meta = this.slotMeta[slot];
      for (const item of plan.meals[slot] ?? []) {
        entries.push({ id: item.id, recipeId: item.recipeId ?? null, time: meta.time, emoji: meta.emoji, name: item.displayName, note: item.note ?? null, participants: item.participants ?? [] });
      }
    }
    return entries;
  }

  private todayString(): string {
    const today = new Date();
    const year = today.getFullYear();
    const month = String(today.getMonth() + 1).padStart(2, '0');
    const day = String(today.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
