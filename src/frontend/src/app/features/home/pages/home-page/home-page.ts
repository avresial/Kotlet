import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { PantryItem } from '../../../pantry/pantry.models';
import { PantryService } from '../../../pantry/pantry.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { IngredientPicker } from '../../../ingredients/components/ingredient-picker/ingredient-picker';
import { ShoppingListItem } from '../../../shopping-list/shopping-list.models';
import { ShoppingListService } from '../../../shopping-list/shopping-list.service';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { RecipeAuditItem, RecipeDetail, RecipeSummary } from '../../../recipes/models/recipe.models';
import { RecipeService } from '../../../recipes/services/recipe.service';
import { DailyMealPlan, MealParticipant, MealSlot } from '../../../meal-planner/models/meal-planner.models';
import { MealPlannerService } from '../../../meal-planner/services/meal-planner.service';
import { DashboardStats, HouseMember } from '../../home.models';
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

interface UselessFact { text: string; source: string; source_url: string; }

export const newestIngredients = (ingredients: Ingredient[]): Ingredient[] =>
  [...ingredients].sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc) || left.name.localeCompare(right.name)).slice(0, 5);

export const ingredientPreview = (recipe: RecipeDetail): string =>
  recipe.ingredients.slice(0, 3).map(ingredient => ingredient.name).join(', ');

@Component({
  selector: 'app-home-page',
  imports: [RouterLink, FormsModule, IngredientPicker, TranslatePipe],
  templateUrl: './home-page.html',
  styleUrl: './home-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage implements OnInit {
  readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly pantryService = inject(PantryService);
  private readonly ingredientService = inject(IngredientService);
  private readonly shoppingListService = inject(ShoppingListService);
  private readonly recipeService = inject(RecipeService);
  private readonly mealPlannerService = inject(MealPlannerService);
  private readonly homeService = inject(HomeService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly translations = inject(TranslationService);
  readonly lowStock = signal<PantryItem[]>([]);
  readonly pantryLoading = signal(true);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly recentIngredients = computed(() => newestIngredients(this.ingredients()));
  readonly shoppingItems = signal<ShoppingListItem[]>([]);
  readonly shoppingLoading = signal(true);
  readonly shoppingSaving = signal(false);
  readonly shoppingError = signal<string | null>(null);
  readonly newestRecipes = signal<RecipeSummary[]>([]);
  readonly auditItems = signal<RecipeAuditItem[]>([]);
  readonly auditLoading = signal(true);
  readonly auditError = signal(false);
  readonly recipeAvatarUrls = signal<Record<string, string>>({});
  readonly recipesLoading = signal(true);
  readonly recipesError = signal(false);
  readonly selectedIngredientId = signal('');
  readonly newQuantity = signal(1);
  readonly availableIngredients = computed(() => this.ingredients().filter(ingredient =>
    !this.shoppingItems().some(item => item.ingredientId === ingredient.id)));
  readonly selectedShoppingIngredient = computed(() =>
    this.ingredients().find(ingredient => ingredient.id === this.selectedIngredientId()));
  readonly purchasedCount = computed(() => this.shoppingItems().filter(item => item.isPurchased).length);
  readonly totalPrice = computed(() => this.shoppingItems().reduce((total, item) => total + item.totalPrice, 0));
  readonly shoppingProgress = computed(() => this.shoppingItems().length
    ? Math.round(this.purchasedCount() / this.shoppingItems().length * 100) : 0);
  readonly firstName = computed(() => {
    const user = this.auth.currentUser();
    return user?.displayName?.trim().split(/\s+/)[0] || user?.email.split('@')[0] || this.translations.translate('home.dashboard.there');
  });
  readonly today = computed(() => new Intl.DateTimeFormat(this.translations.language(), {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
  }).format(new Date()));

  readonly todaysMenu = signal<TodaysMenuEntry[]>([]);
  readonly menuAvatarUrls = signal<Record<string, string>>({});
  readonly menuRecipes = signal<Record<string, RecipeDetail>>({});
  readonly menuLoading = signal(true);
  readonly menuError = signal(false);

  readonly houseName = signal<string | null>(null);
  readonly houseMembers = signal<HouseMember[]>([]);
  readonly houseLoading = signal(true);
  readonly houseError = signal(false);
  readonly uselessFact = signal<UselessFact | null>(null);
  readonly factLoading = signal(true);
  readonly factError = signal(false);
  readonly stats = signal<DashboardStats | null>(null);
  readonly statsLoading = signal(true);
  readonly statsError = signal(false);

  private readonly slotMeta: Record<MealSlot, { emoji: string }> = {
    breakfast: { emoji: '🍳' },
    'second-breakfast': { emoji: '🥪' },
    dinner: { emoji: '🥪' },
    snack: { emoji: '🍎' },
    supper: { emoji: '🍽️' },
  };
  private readonly slotOrder: MealSlot[] = ['breakfast', 'second-breakfast', 'dinner', 'snack', 'supper'];

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
    this.recipeService.listAudit(5).pipe(finalize(() => this.auditLoading.set(false))).subscribe({
      next: items => this.auditItems.set(items),
      error: () => this.auditError.set(true),
    });
    this.homeService.getDashboardStats().pipe(finalize(() => this.statsLoading.set(false))).subscribe({
      next: stats => this.stats.set(stats),
      error: () => this.statsError.set(true),
    });
    this.mealPlannerService.getForDate(this.todayString()).pipe(finalize(() => this.menuLoading.set(false))).subscribe({
      next: plan => {
        const menu = this.buildMenu(plan);
        this.todaysMenu.set(menu);
        this.loadMenuDetails(menu);
      },
      error: () => this.menuError.set(true),
    });
    const activeHouseId = this.auth.currentUser()?.activeHouseId;
    if (activeHouseId) {
      this.homeService.getHome(activeHouseId).pipe(finalize(() => this.houseLoading.set(false))).subscribe({
        next: house => { this.houseName.set(house.name); this.houseMembers.set(house.members); },
        error: () => this.houseError.set(true),
      });
    } else {
      this.houseLoading.set(false);
    }
    forkJoin({ pantry: this.pantryService.getAll(), ingredients: this.ingredientService.getAll(), shopping: this.shoppingListService.getAll() })
      .pipe(finalize(() => { this.pantryLoading.set(false); this.shoppingLoading.set(false); }))
      .subscribe({
        next: ({ pantry, ingredients, shopping }) => {
          this.lowStock.set(pantry.slice(0, 5));
          this.ingredients.set(ingredients);
          this.shoppingItems.set(shopping);
        },
        error: error => this.shoppingError.set(getApiError(error, this.translations.translate('home.dashboard.loadError'))),
      });
    this.loadFact();
  }

  loadFact(): void {
    this.factLoading.set(true);
    this.factError.set(false);
    this.http.get<UselessFact>('https://uselessfacts.jsph.pl/api/v2/facts/random?language=en')
      .pipe(finalize(() => this.factLoading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: fact => this.uselessFact.set(fact), error: () => this.factError.set(true) });
  }

  relativeTime(dateStr: string): string {
    const diff = Date.now() - new Date(dateStr).getTime();
    const seconds = Math.floor(diff / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    const weeks = Math.floor(days / 7);
    const months = Math.floor(days / 30);
    const rtf = new Intl.RelativeTimeFormat(this.translations.language(), { numeric: 'auto' });
    if (months >= 1) return rtf.format(-months, 'month');
    if (weeks >= 1) return rtf.format(-weeks, 'week');
    if (days >= 1) return rtf.format(-days, 'day');
    if (hours >= 1) return rtf.format(-hours, 'hour');
    if (minutes >= 1) return rtf.format(-minutes, 'minute');
    return this.translations.translate('home.dashboard.justNow');
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

  private loadMenuDetails(entries: TodaysMenuEntry[]): void {
    const recipeIds = [...new Set(entries.map(entry => entry.recipeId).filter((id): id is string => !!id))];
    for (const recipeId of recipeIds) {
      this.recipeService.get(recipeId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(recipe => {
          this.menuRecipes.update(recipes => ({ ...recipes, [recipeId]: recipe }));
          const first = recipe.images[0];
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

  menuIngredientPreview(recipeId: string): string { return ingredientPreview(this.menuRecipes()[recipeId]); }

  relativeDate(value: string): string {
    const elapsedDays = Math.max(0, Math.floor((Date.now() - new Date(value).getTime()) / 86_400_000));
    if (elapsedDays === 0) return this.translations.translate('home.dashboard.today');
    if (elapsedDays === 1) return this.translations.translate('home.dashboard.yesterday');
    if (elapsedDays < 7) return this.translations.translate('home.dashboard.daysAgo').replace('{count}', String(elapsedDays));
    const weeks = Math.floor(elapsedDays / 7);
    if (weeks < 5) return this.translations.translate(weeks === 1 ? 'home.dashboard.weekAgo' : 'home.dashboard.weeksAgo').replace('{count}', String(weeks));
    return new Intl.DateTimeFormat(this.translations.language(), { month: 'short', day: 'numeric' }).format(new Date(value));
  }

  addToShoppingList(ingredientId = this.selectedIngredientId(), quantity = this.newQuantity()): void {
    if (!ingredientId || !Number.isFinite(quantity) || quantity <= 0 || this.shoppingSaving()) return;
    this.shoppingSaving.set(true); this.shoppingError.set(null);
    this.shoppingListService.create(ingredientId, quantity).pipe(finalize(() => this.shoppingSaving.set(false))).subscribe({
      next: item => {
        this.shoppingItems.update(items => [...items, item]);
        this.selectedIngredientId.set(''); this.newQuantity.set(1);
      },
      error: error => this.shoppingError.set(getApiError(error, this.translations.translate('shopping.addError'))),
    });
  }

  updateItem(item: ShoppingListItem, changes: Partial<Pick<ShoppingListItem, 'quantity' | 'isPurchased'>>): void {
    if (changes.quantity !== undefined && (!Number.isFinite(changes.quantity) || changes.quantity <= 0)) return;
    this.shoppingSaving.set(true); this.shoppingError.set(null);
    this.shoppingListService.update(item, changes).pipe(finalize(() => this.shoppingSaving.set(false))).subscribe({
      next: updated => this.shoppingItems.update(items => items.map(current => current.id === updated.id ? updated : current)),
      error: error => this.shoppingError.set(getApiError(error, this.translations.translate('shopping.updateError'))),
    });
  }

  removeItem(item: ShoppingListItem): void {
    this.shoppingSaving.set(true); this.shoppingError.set(null);
    this.shoppingListService.delete(item.id).pipe(finalize(() => this.shoppingSaving.set(false))).subscribe({
      next: () => this.shoppingItems.update(items => items.filter(current => current.id !== item.id)),
      error: error => this.shoppingError.set(getApiError(error, this.translations.translate('shopping.removeError'))),
    });
  }

  clearChecked(): void {
    if (this.shoppingSaving() || this.purchasedCount() === 0) return;
    this.shoppingSaving.set(true); this.shoppingError.set(null);
    this.shoppingListService.clearChecked().pipe(finalize(() => this.shoppingSaving.set(false))).subscribe({
      next: () => this.shoppingItems.update(items => items.filter(item => !item.isPurchased)),
      error: error => this.shoppingError.set(getApiError(error, this.translations.translate('shopping.clearError'))),
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
        entries.push({ id: item.id, recipeId: item.recipeId ?? null, time: this.translations.translate(`meal.slot.${slot}`), emoji: meta.emoji, name: item.displayName, note: item.note ?? null, participants: item.participants ?? [] });
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
