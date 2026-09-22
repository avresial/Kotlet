import { HttpClient } from '@angular/common/http';
import { signal, WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../../core/auth/auth.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { DailyMealPlan } from '../../../meal-planner/models/meal-planner.models';
import { MealPlannerService } from '../../../meal-planner/services/meal-planner.service';
import { PantryService } from '../../../pantry/pantry.service';
import { RecipeDetail } from '../../../recipes/models/recipe.models';
import { RecipeService } from '../../../recipes/services/recipe.service';
import { ShoppingListService } from '../../../shopping-list/shopping-list.service';
import { dashboardCacheKey, writeDashboardCache } from '../../dashboard-cache';
import { HomeService } from '../../home.service';
import { DashboardStats } from '../../home.models';
import { addLocalDays, HomePage, ingredientPreview, localDayOffset, newestIngredients } from './home-page';

describe('ingredientPreview', () => {
  it('shows at most three ingredient names', () => {
    const recipe = { ingredients: ['Tomato', 'Garlic', 'Cream', 'Salt'].map(name => ({ name })) } as RecipeDetail;
    expect(ingredientPreview(recipe)).toBe('Tomato, Garlic, Cream');
  });
});

describe('newestIngredients', () => {
  it('returns the five newest without mutating the source', () => {
    const ingredients = Array.from({ length: 6 }, (_, index) => ({ name: String(index), createdAtUtc: `2026-01-0${index + 1}` })) as Ingredient[];
    expect(newestIngredients(ingredients).map(item => item.name)).toEqual(['5', '4', '3', '2', '1']);
    expect(ingredients[0].name).toBe('0');
  });
});

describe('dashboard date navigation', () => {
  const emptyPlan = { meals: {} } as DailyMealPlan;
  const getForDate = vi.fn(() => of(emptyPlan));
  let page: HomePage;

  beforeEach(() => {
    getForDate.mockClear();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { currentUser: signal(null) } },
        { provide: HttpClient, useValue: { get: () => of({}) } },
        { provide: PantryService, useValue: { getAll: () => of([]) } },
        { provide: IngredientService, useValue: { getAll: () => of([]) } },
        { provide: ShoppingListService, useValue: { getAll: () => of([]) } },
        { provide: RecipeService, useValue: { listRecent: () => of([]), listAudit: () => of([]) } },
        { provide: MealPlannerService, useValue: { getForDate } },
        { provide: HomeService, useValue: { getDashboardStats: () => of({}) } },
        {
          provide: TranslationService,
          useValue: { language: signal('en'), translate: (key: string) => key },
        },
      ],
    });
    page = TestBed.runInInjectionContext(() => new HomePage());
  });

  afterEach(() => {
    vi.useRealTimers();
    localStorage.clear();
  });

  it('requests adjacent local calendar days', () => {
    const today = page.selectedDate();

    page.goToPreviousDay();
    expect(getForDate).toHaveBeenLastCalledWith(addLocalDays(today, -1));

    page.goToNextDay();
    expect(getForDate).toHaveBeenLastCalledWith(today);
  });

  it('stops navigation at the seven-day boundaries', () => {
    const today = page.selectedDate();
    const minimum = addLocalDays(today, -7);
    page.selectedDate.set(minimum);

    expect(page.canGoToPreviousDay()).toBe(false);
    page.goToPreviousDay();
    expect(page.selectedDate()).toBe(minimum);
    expect(getForDate).not.toHaveBeenCalled();

    const maximum = addLocalDays(today, 7);
    page.selectedDate.set(maximum);
    expect(page.canGoToNextDay()).toBe(false);
    page.goToNextDay();
    expect(page.selectedDate()).toBe(maximum);
    expect(getForDate).not.toHaveBeenCalled();
  });

  it('returns to today and restores the today state', () => {
    const today = page.selectedDate();
    page.selectedDate.set(addLocalDays(today, 3));

    page.goToToday();

    expect(page.selectedDate()).toBe(today);
    expect(page.isSelectedDateToday()).toBe(true);
    expect(getForDate).toHaveBeenLastCalledWith(today);
  });

  it('refreshes the today baseline at local midnight', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 7, 11, 23, 59, 59, 900));
    page = TestBed.runInInjectionContext(() => new HomePage());
    page.ngOnInit();
    page.selectedDate.set('2026-08-11');
    getForDate.mockClear();

    vi.advanceTimersByTime(100);

    expect(page.selectedDateRelativeLabel()).toBe('home.dashboard.yesterday');
    page.goToToday();
    expect(page.selectedDate()).toBe('2026-08-12');
    expect(getForDate).toHaveBeenLastCalledWith('2026-08-12');
  });

  it('clamps and reloads a selected date outside the refreshed range', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 7, 11, 23, 59, 59, 900));
    page = TestBed.runInInjectionContext(() => new HomePage());
    page.ngOnInit();
    page.selectedDate.set('2026-08-04');
    getForDate.mockClear();

    vi.advanceTimersByTime(100);

    expect(page.selectedDate()).toBe('2026-08-05');
    expect(page.canGoToPreviousDay()).toBe(false);
    expect(getForDate).toHaveBeenCalledOnce();
    expect(getForDate).toHaveBeenCalledWith('2026-08-05');
  });

  it('calculates offsets by calendar date across daylight-saving changes', () => {
    expect(localDayOffset('2026-03-28', '2026-03-30')).toBe(2);
    expect(addLocalDays('2026-03-29', 1)).toBe('2026-03-30');
  });
});

describe('dashboard cache', () => {
  const user = { id: 'user-1', activeHouseId: 'house-1' };
  const cachedStats: DashboardStats = { recipeCount: 2, pantryItemCount: 3 };
  let currentUser: WritableSignal<typeof user>;
  let statsResponse: Subject<DashboardStats>;
  let getDashboardStats: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    localStorage.clear();
    currentUser = signal(user);
    statsResponse = new Subject<DashboardStats>();
    getDashboardStats = vi.fn(() => statsResponse.asObservable());
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { currentUser } },
        { provide: HttpClient, useValue: { get: () => of({ text: 'Fact', source: 'Source', source_url: 'https://example.com' }) } },
        { provide: PantryService, useValue: { getAll: () => of([]) } },
        { provide: IngredientService, useValue: { getAll: () => of([]) } },
        { provide: ShoppingListService, useValue: { getAll: () => of([]) } },
        { provide: RecipeService, useValue: { listRecent: () => of([]), listAudit: () => of([]) } },
        { provide: MealPlannerService, useValue: { getForDate: () => of({ meals: {} }) } },
        { provide: HomeService, useValue: { getDashboardStats, getHome: () => of({ name: 'Home', members: [] }) } },
        {
          provide: TranslationService,
          useValue: { language: signal('en'), translate: (key: string) => key },
        },
      ],
    });
  });

  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  const createPage = (): HomePage => TestBed.runInInjectionContext(() => new HomePage());

  it('scopes the cache to the authenticated user and active household', () => {
    expect(dashboardCacheKey(user)).toBe('kotlet.dashboard.user-1:house-1');
    expect(dashboardCacheKey({ ...user, id: 'user-2' })).not.toBe(dashboardCacheKey(user));
    expect(dashboardCacheKey({ ...user, activeHouseId: 'house-2' })).not.toBe(dashboardCacheKey(user));
    expect(dashboardCacheKey(null)).toBeNull();
  });

  it('keeps the normal loading state when no cache exists', () => {
    const page = createPage();

    page.ngOnInit();

    expect(page.statsLoading()).toBe(true);
    statsResponse.next(cachedStats);
    statsResponse.complete();
    expect(page.statsLoading()).toBe(false);
  });

  it('renders cached stats before the refresh and keeps the same value when unchanged', () => {
    const key = dashboardCacheKey(user);
    if (!key) throw new Error('Expected a dashboard cache key.');
    writeDashboardCache(key, { stats: cachedStats });
    const page = createPage();

    page.ngOnInit();

    const renderedStats = page.stats();
    const storedCache = localStorage.getItem(key);
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    expect(renderedStats).toEqual(cachedStats);
    expect(page.statsLoading()).toBe(false);
    expect(getDashboardStats).toHaveBeenCalledOnce();

    statsResponse.next({ ...cachedStats });
    statsResponse.complete();

    expect(page.stats()).toBe(renderedStats);
    expect(setItem).not.toHaveBeenCalled();
    expect(localStorage.getItem(key)).toBe(storedCache);
  });

  it('updates the rendered stats and cache when fresh data changes', () => {
    const key = dashboardCacheKey(user);
    if (!key) throw new Error('Expected a dashboard cache key.');
    writeDashboardCache(key, { stats: cachedStats });
    const page = createPage();
    page.ngOnInit();

    statsResponse.next({ recipeCount: 4, pantryItemCount: 5 });
    statsResponse.complete();

    expect(page.stats()).toEqual({ recipeCount: 4, pantryItemCount: 5 });
    expect(JSON.parse(localStorage.getItem(key) ?? '{}').stats).toEqual({ recipeCount: 4, pantryItemCount: 5 });
  });

  it('keeps cached stats visible and reports a refresh failure', () => {
    const key = dashboardCacheKey(user);
    if (!key) throw new Error('Expected a dashboard cache key.');
    writeDashboardCache(key, { stats: cachedStats });
    getDashboardStats.mockReturnValue(throwError(() => new Error('offline')));
    const page = createPage();

    page.ngOnInit();

    expect(page.stats()).toEqual(cachedStats);
    expect(page.statsLoading()).toBe(false);
    expect(page.statsError()).toBe(true);
    expect(JSON.parse(localStorage.getItem(key) ?? '{}').stats).toEqual(cachedStats);
  });

  it('ignores malformed cached menu data and loads the fresh plan', () => {
    const page = createPage();
    const key = dashboardCacheKey(user);
    if (!key) throw new Error('Expected a dashboard cache key.');
    writeDashboardCache(key, {
      menu: {
        date: page.selectedDate(),
        plan: { meals: { breakfast: null } } as unknown as DailyMealPlan,
      },
    });

    expect(() => page.ngOnInit()).not.toThrow();
    expect(page.todaysMenu()).toEqual([]);
  });

  it('replaces restored content when the authenticated user or household changes', () => {
    const nextUser = { id: 'user-2', activeHouseId: 'house-2' };
    const nextStats = { recipeCount: 8, pantryItemCount: 9 };
    const firstKey = dashboardCacheKey(user);
    const nextKey = dashboardCacheKey(nextUser);
    if (!firstKey || !nextKey) throw new Error('Expected dashboard cache keys.');
    writeDashboardCache(firstKey, { stats: cachedStats });
    writeDashboardCache(nextKey, { stats: nextStats });
    const page = createPage();

    page.ngOnInit();
    expect(page.stats()).toEqual(cachedStats);

    currentUser.set(nextUser);
    TestBed.flushEffects();

    expect(page.stats()).toEqual(nextStats);
    expect(page.stats()).not.toEqual(cachedStats);
    expect(getDashboardStats).toHaveBeenCalledTimes(2);
  });
});
