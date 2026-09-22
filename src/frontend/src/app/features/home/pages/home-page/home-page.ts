import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, OnInit, signal, WritableSignal } from '@angular/core';
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
import {
  DashboardCacheSnapshot,
  DashboardFact,
  dashboardCacheKey as getDashboardCacheKey,
  readDashboardCache,
  writeDashboardCache,
} from '../../dashboard-cache';

interface TodaysMenuEntry {
  id: string;
  recipeId: string | null;
  time: string;
  emoji: string;
  name: string;
  note: string | null;
  participants: MealParticipant[];
}

export const newestIngredients = (ingredients: Ingredient[]): Ingredient[] =>
  [...ingredients].sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc) || left.name.localeCompare(right.name)).slice(0, 5);

export const ingredientPreview = (recipe: RecipeDetail): string =>
  recipe.ingredients.slice(0, 3).map(ingredient => ingredient.name).join(', ');

export const localDateString = (date: Date): string => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

export const addLocalDays = (value: string, days: number): string => {
  const [year, month, day] = value.split('-').map(Number);
  const date = new Date(year, month - 1, day);
  date.setDate(date.getDate() + days);
  return localDateString(date);
};

export const localDayOffset = (from: string, to: string): number => {
  const [fromYear, fromMonth, fromDay] = from.split('-').map(Number);
  const [toYear, toMonth, toDay] = to.split('-').map(Number);
  return Math.round(
    (Date.UTC(toYear, toMonth - 1, toDay) - Date.UTC(fromYear, fromMonth - 1, fromDay)) / 86_400_000,
  );
};

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
  private readonly dashboardToday = signal(localDateString(new Date()));
  readonly today = computed(() => new Intl.DateTimeFormat(this.translations.language(), {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
  }).format(this.localDate(this.dashboardToday())));

  readonly todaysMenu = signal<TodaysMenuEntry[]>([]);
  readonly menuAvatarUrls = signal<Record<string, string>>({});
  readonly menuRecipes = signal<Record<string, RecipeDetail>>({});
  readonly menuLoading = signal(true);
  readonly menuError = signal(false);
  private readonly maximumDayOffset = 7;
  private menuRequestId = 0;
  private dashboardDayRefreshTimer: ReturnType<typeof setTimeout> | undefined;
  readonly selectedDate = signal(this.dashboardToday());
  readonly selectedDateOffset = computed(() => localDayOffset(this.dashboardToday(), this.selectedDate()));
  readonly selectedDateLabel = computed(() => {
    const [year, month, day] = this.selectedDate().split('-').map(Number);
    return new Intl.DateTimeFormat(this.translations.language(), {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    }).format(new Date(year, month - 1, day));
  });
  readonly selectedDateRelativeLabel = computed(() => {
    switch (this.selectedDateOffset()) {
      case -1:
        return this.translations.translate('home.dashboard.yesterday');
      case 0:
        return this.translations.translate('home.dashboard.today');
      case 1:
        return this.translations.translate('home.dashboard.tomorrow');
      default:
        return '';
    }
  });
  readonly isSelectedDateToday = computed(() => this.selectedDateOffset() === 0);
  readonly canGoToPreviousDay = computed(() => this.selectedDateOffset() > -this.maximumDayOffset);
  readonly canGoToNextDay = computed(() => this.selectedDateOffset() < this.maximumDayOffset);

  readonly houseName = signal<string | null>(null);
  readonly houseMembers = signal<HouseMember[]>([]);
  readonly houseLoading = signal(true);
  readonly houseError = signal(false);
  readonly uselessFact = signal<DashboardFact | null>(null);
  readonly factLoading = signal(true);
  readonly factError = signal(false);
  readonly stats = signal<DashboardStats | null>(null);
  readonly statsLoading = signal(true);
  readonly statsError = signal(false);
  private dashboardCacheKey: string | null = null;
  private dashboardCache: DashboardCacheSnapshot | null = null;
  private dashboardInitialized = false;
  private dashboardLoadId = 0;

  private readonly slotMeta: Record<MealSlot, { emoji: string }> = {
    breakfast: { emoji: '🍳' },
    'second-breakfast': { emoji: '🥪' },
    dinner: { emoji: '🥪' },
    snack: { emoji: '🍎' },
    supper: { emoji: '🍽️' },
  };
  private readonly slotOrder: MealSlot[] = ['breakfast', 'second-breakfast', 'dinner', 'snack', 'supper'];

  constructor() {
    effect(() => {
      const cacheKey = getDashboardCacheKey(this.auth.currentUser());
      if (!this.dashboardInitialized || cacheKey === this.dashboardCacheKey) return;

      this.dashboardCacheKey = cacheKey;
      this.dashboardCache = cacheKey ? readDashboardCache(cacheKey) : null;
      this.clearDashboardState();
      const hasCachedMenu = this.restoreDashboardCache();
      this.loadDashboardData(hasCachedMenu);
    });
  }

  ngOnInit(): void {
    this.dashboardCacheKey = getDashboardCacheKey(this.auth.currentUser());
    this.dashboardCache = this.dashboardCacheKey ? readDashboardCache(this.dashboardCacheKey) : null;
    const hasCachedMenu = this.restoreDashboardCache();
    this.dashboardInitialized = true;
    this.destroyRef.onDestroy(() => {
      if (this.dashboardDayRefreshTimer !== undefined) {
        clearTimeout(this.dashboardDayRefreshTimer);
      }
      Object.values(this.recipeAvatarUrls()).forEach(url => URL.revokeObjectURL(url));
      Object.values(this.menuAvatarUrls()).forEach(url => URL.revokeObjectURL(url));
    });
    this.scheduleDashboardDayRefresh();
    this.loadDashboardData(hasCachedMenu);
  }

  private loadDashboardData(keepCachedMenu = false): void {
    const loadId = ++this.dashboardLoadId;
    this.recipeService.listRecent(4).pipe(finalize(() => {
      if (loadId === this.dashboardLoadId) this.recipesLoading.set(false);
    })).subscribe({
      next: recipes => {
        if (loadId !== this.dashboardLoadId) return;
        const changed = this.setIfChanged(this.newestRecipes, recipes);
        this.saveDashboardCache({ recipes });
        if (changed) this.loadAvatars(recipes);
      },
      error: () => {
        if (loadId === this.dashboardLoadId) this.recipesError.set(true);
      },
    });
    this.recipeService.listAudit(5).pipe(finalize(() => {
      if (loadId === this.dashboardLoadId) this.auditLoading.set(false);
    })).subscribe({
      next: items => {
        if (loadId !== this.dashboardLoadId) return;
        this.setIfChanged(this.auditItems, items);
        this.saveDashboardCache({ audit: items });
      },
      error: () => {
        if (loadId === this.dashboardLoadId) this.auditError.set(true);
      },
    });
    this.homeService.getDashboardStats().pipe(finalize(() => {
      if (loadId === this.dashboardLoadId) this.statsLoading.set(false);
    })).subscribe({
      next: stats => {
        if (loadId !== this.dashboardLoadId) return;
        this.setIfChanged(this.stats, stats);
        this.saveDashboardCache({ stats });
      },
      error: () => {
        if (loadId === this.dashboardLoadId) this.statsError.set(true);
      },
    });
    this.loadMenu(this.selectedDate(), keepCachedMenu);
    const activeHouseId = this.auth.currentUser()?.activeHouseId;
    if (activeHouseId) {
      this.homeService.getHome(activeHouseId).pipe(finalize(() => {
        if (loadId === this.dashboardLoadId) this.houseLoading.set(false);
      })).subscribe({
        next: house => {
          if (loadId !== this.dashboardLoadId) return;
          this.setIfChanged(this.houseName, house.name);
          this.setIfChanged(this.houseMembers, house.members);
          this.saveDashboardCache({ home: house });
        },
        error: () => {
          if (loadId === this.dashboardLoadId) this.houseError.set(true);
        },
      });
    } else {
      this.houseLoading.set(false);
    }
    forkJoin({ pantry: this.pantryService.getAll(), ingredients: this.ingredientService.getAll(), shopping: this.shoppingListService.getAll() })
      .pipe(finalize(() => {
        if (loadId === this.dashboardLoadId) {
          this.pantryLoading.set(false);
          this.shoppingLoading.set(false);
        }
      }))
      .subscribe({
        next: ({ pantry, ingredients, shopping }) => {
          if (loadId !== this.dashboardLoadId) return;
          this.setIfChanged(this.lowStock, pantry.slice(0, 5));
          this.setIfChanged(this.ingredients, ingredients);
          this.setIfChanged(this.shoppingItems, shopping);
          this.saveDashboardCache({ lowStock: pantry.slice(0, 5), ingredients, shoppingItems: shopping });
        },
        error: error => {
          if (loadId === this.dashboardLoadId) {
            this.shoppingError.set(getApiError(error, this.translations.translate('home.dashboard.loadError')));
          }
        },
      });
    this.loadFact(loadId);
  }

  private restoreDashboardCache(): boolean {
    const cached = this.dashboardCache;
    if (!cached) return false;

    if (cached.stats !== undefined) {
      this.stats.set(cached.stats);
      this.statsLoading.set(false);
    }
    if (cached.recipes !== undefined) {
      this.newestRecipes.set(cached.recipes);
      this.recipesLoading.set(false);
    }
    if (cached.audit !== undefined) {
      this.auditItems.set(cached.audit);
      this.auditLoading.set(false);
    }
    if (cached.shoppingItems !== undefined) {
      this.shoppingItems.set(cached.shoppingItems);
      this.shoppingLoading.set(false);
    }
    if (cached.lowStock !== undefined) {
      this.lowStock.set(cached.lowStock);
      this.pantryLoading.set(false);
    }
    if (cached.ingredients !== undefined) {
      this.ingredients.set(cached.ingredients);
    }
    if (cached.home !== undefined) {
      this.houseName.set(cached.home.name);
      this.houseMembers.set(cached.home.members);
      this.houseLoading.set(false);
    }
    if (cached.fact !== undefined) {
      this.uselessFact.set(cached.fact);
      this.factLoading.set(false);
    }

    const cachedMenu = cached.menu;
    if (!this.isValidCachedMenu(cachedMenu)) return false;
    this.todaysMenu.set(this.buildMenu(cachedMenu.plan));
    this.menuLoading.set(false);
    return true;
  }

  private isValidCachedMenu(menu: DashboardCacheSnapshot['menu']): menu is NonNullable<DashboardCacheSnapshot['menu']> {
    if (!menu || menu.date !== this.selectedDate() || !menu.plan || !menu.plan.meals || typeof menu.plan.meals !== 'object') return false;
    return this.slotOrder.every(slot => Array.isArray(menu.plan.meals[slot]));
  }

  private clearDashboardState(): void {
    Object.values(this.recipeAvatarUrls()).forEach(url => URL.revokeObjectURL(url));
    this.recipeAvatarUrls.set({});
    this.clearMenu();
    this.menuLoading.set(true);
    this.stats.set(null);
    this.statsLoading.set(true);
    this.statsError.set(false);
    this.newestRecipes.set([]);
    this.recipesLoading.set(true);
    this.recipesError.set(false);
    this.auditItems.set([]);
    this.auditLoading.set(true);
    this.auditError.set(false);
    this.shoppingItems.set([]);
    this.shoppingLoading.set(true);
    this.shoppingError.set(null);
    this.lowStock.set([]);
    this.pantryLoading.set(true);
    this.ingredients.set([]);
    this.houseName.set(null);
    this.houseMembers.set([]);
    this.houseLoading.set(true);
    this.houseError.set(false);
    this.uselessFact.set(null);
    this.factLoading.set(true);
    this.factError.set(false);
  }

  private setIfChanged<T>(target: WritableSignal<T>, value: T): boolean {
    if (JSON.stringify(target()) === JSON.stringify(value)) return false;
    target.set(value);
    return true;
  }

  private saveDashboardCache(update: Partial<DashboardCacheSnapshot>): void {
    if (!this.dashboardCacheKey) return;
    const changed = Object.entries(update).some(([key, value]) =>
      JSON.stringify(this.dashboardCache?.[key as keyof DashboardCacheSnapshot]) !== JSON.stringify(value));
    if (!changed) return;
    const next = { ...(this.dashboardCache ?? {}), ...update };
    this.dashboardCache = next;
    writeDashboardCache(this.dashboardCacheKey, this.dashboardCache);
  }

  loadFact(loadId = this.dashboardLoadId): void {
    this.factLoading.set(true);
    this.factError.set(false);
    this.http.get<DashboardFact>('https://uselessfacts.jsph.pl/api/v2/facts/random?language=en')
      .pipe(finalize(() => {
        if (loadId === this.dashboardLoadId) this.factLoading.set(false);
      }), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: fact => {
          if (loadId !== this.dashboardLoadId) return;
          this.setIfChanged(this.uselessFact, fact);
          this.saveDashboardCache({ fact });
        },
        error: () => {
          if (loadId === this.dashboardLoadId) this.factError.set(true);
        },
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
    const rtf = new Intl.RelativeTimeFormat(this.translations.language(), { numeric: 'auto' });
    if (months >= 1) return rtf.format(-months, 'month');
    if (weeks >= 1) return rtf.format(-weeks, 'week');
    if (days >= 1) return rtf.format(-days, 'day');
    if (hours >= 1) return rtf.format(-hours, 'hour');
    if (minutes >= 1) return rtf.format(-minutes, 'minute');
    return this.translations.translate('home.dashboard.justNow');
  }

  goToPreviousDay(): void {
    if (this.canGoToPreviousDay()) this.selectDate(addLocalDays(this.selectedDate(), -1));
  }

  goToNextDay(): void {
    if (this.canGoToNextDay()) this.selectDate(addLocalDays(this.selectedDate(), 1));
  }

  goToToday(): void {
    this.selectDate(this.dashboardToday());
  }

  private scheduleDashboardDayRefresh(): void {
    const now = new Date();
    const nextMidnight = new Date(now);
    nextMidnight.setHours(24, 0, 0, 0);
    this.dashboardDayRefreshTimer = setTimeout(() => {
      this.refreshDashboardDay();
      this.scheduleDashboardDayRefresh();
    }, nextMidnight.getTime() - now.getTime());
  }

  private refreshDashboardDay(): void {
    const newToday = localDateString(new Date());
    if (newToday === this.dashboardToday()) {
      return;
    }

    const selectedOffset = localDayOffset(newToday, this.selectedDate());
    const clampedOffset = Math.max(-this.maximumDayOffset, Math.min(this.maximumDayOffset, selectedOffset));
    const clampedDate = addLocalDays(newToday, clampedOffset);
    this.dashboardToday.set(newToday);
    this.selectedDate.set(clampedDate);
    this.loadMenu(clampedDate);
  }

  private localDate(value: string): Date {
    const [year, month, day] = value.split('-').map(Number);
    return new Date(year, month - 1, day);
  }

  private selectDate(date: string): void {
    if (date === this.selectedDate()) return;
    this.selectedDate.set(date);
    this.loadMenu(date);
  }

  private loadMenu(date: string, keepCachedState = false): void {
    const requestId = ++this.menuRequestId;
    if (keepCachedState) {
      this.menuError.set(false);
    } else {
      this.clearMenu();
      this.menuLoading.set(true);
    }
    this.mealPlannerService.getForDate(date).pipe(
      finalize(() => {
        if (requestId === this.menuRequestId) this.menuLoading.set(false);
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: plan => {
        if (requestId !== this.menuRequestId) return;
        const menu = this.buildMenu(plan);
        this.setIfChanged(this.todaysMenu, menu);
        this.saveDashboardCache({ menu: { date, plan } });
        this.loadMenuDetails(menu, requestId);
      },
      error: () => {
        if (requestId === this.menuRequestId) this.menuError.set(true);
      },
    });
  }

  private clearMenu(): void {
    Object.values(this.menuAvatarUrls()).forEach(url => URL.revokeObjectURL(url));
    this.todaysMenu.set([]);
    this.menuAvatarUrls.set({});
    this.menuRecipes.set({});
    this.menuError.set(false);
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

  private loadMenuDetails(entries: TodaysMenuEntry[], requestId: number): void {
    const recipeIds = [...new Set(entries.map(entry => entry.recipeId).filter((id): id is string => !!id))];
    for (const recipeId of recipeIds) {
      this.recipeService.get(recipeId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(recipe => {
          if (requestId !== this.menuRequestId) return;
          this.menuRecipes.update(recipes => ({ ...recipes, [recipeId]: recipe }));
          const first = recipe.images[0];
          if (!first) return;
          this.recipeService.imageContent(first.contentUrl)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe(blob => {
              if (requestId !== this.menuRequestId) return;
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
    return this.initials(this.memberName(member));
  }

  participantInitials(displayName: string): string {
    return this.initials(displayName);
  }

  private initials(value: string): string {
    const name = value.trim();
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

}
