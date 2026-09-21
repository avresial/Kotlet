import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { Observable, of, Subject } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../../core/auth/auth.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { PreparedMealService } from '../../../prepared-meals/prepared-meal.service';
import { RecipeService } from '../../../recipes/services/recipe.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { DailyMealPlan, MealParticipant, MealPlanItem, MealPlanOverviewDay } from '../../models/meal-planner.models';
import { MealPlannerService } from '../../services/meal-planner.service';
import { ShoppingListIntegrationService } from '../../services/shopping-list-integration.service';
import { isValidDateString, MealPlannerPage, mealPlannerCacheKey, mealPlannerCacheStorageKey, weekStart } from './meal-planner-page';

describe('weekStart', () => {
  it.each([
    ['2026-07-02', '2026-06-29'],
    ['2026-07-05', '2026-06-29'],
    ['2026-07-06', '2026-07-06'],
  ])('maps %s to Monday %s', (date, monday) => expect(weekStart(date)).toBe(monday));
});

const ingredient: Ingredient = {
  id: 'eggs',
  name: 'Eggs',
  defaultName: 'Eggs',
  translation: null,
  measurementUnit: 'g',
  isCountable: true,
  measurementUnitsPerPiece: 50,
  caloriesPer100BaseUnits: 155,
  pricePer100BaseUnits: 50,
  svgIcon: null,
  category: 0,
  allergens: 0,
  attributes: 0,
  suitability: 0,
  isAiModified: false,
  createdAtUtc: '2026-01-01T00:00:00Z',
};

// One piece (50 g) of eggs → 155 * 50 / 100 = 77.5 kcal per standard serving.
const caloriesPerServing = 77.5;

const participant: MealParticipant = { userId: 'asik', displayName: 'Asik', isCurrentUser: false, portionPercent: 100 };

const item: MealPlanItem = {
  id: 'item-1',
  slot: 'breakfast',
  type: 'ingredient',
  ingredientId: ingredient.id,
  displayName: 'Eggs',
  sortOrder: 0,
  participants: [participant],
  guests: 0,
  servings: 1,
  servingsOverridden: false,
};

const emptyMeals = () => ({ breakfast: [], 'second-breakfast': [], dinner: [], snack: [], supper: [] });
const plan: DailyMealPlan = { date: '2026-07-13', meals: { ...emptyMeals(), breakfast: [item] } };

describe('MealPlannerPage localStorage cache', () => {
  const user = { id: 'user-1', activeHouseId: 'house-1' };
  const cacheDate = '2026-07-13';

  function storageKey(cacheUser = user): string {
    const key = mealPlannerCacheKey(cacheUser);
    if (!key) throw new Error('Expected an authenticated house user.');
    return mealPlannerCacheStorageKey(key);
  }

  function seedCache(cachedPlan: DailyMealPlan = plan, cachedWeek = weekStart(cachedPlan.date), cacheUser = user): void {
    const key = mealPlannerCacheKey(cacheUser);
    if (!key) throw new Error('Expected an authenticated house user.');
    localStorage.setItem(storageKey(cacheUser), JSON.stringify({
      version: 1,
      userKey: key,
      weekStart: cachedWeek,
      plans: { [cachedPlan.date]: cachedPlan },
    }));
  }

  function createPage(getForDate: () => Observable<DailyMealPlan>, currentUser = user): MealPlannerPage {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { currentUser: () => currentUser } },
        { provide: MealPlannerService, useValue: { getForDate, getHouseMembers: () => of([]), getOverview: () => of([]) } },
        { provide: RecipeService, useValue: { list: () => of({ items: [], total: 0 }), get: () => of(null) } },
        { provide: IngredientService, useValue: { getAll: () => of([]) } },
        { provide: PreparedMealService, useValue: { list: () => of([]) } },
        { provide: ShoppingListIntegrationService, useValue: { addToShoppingList: () => of(null) } },
        { provide: TranslationService, useValue: { translate: (key: string) => key, language: () => 'en' } },
      ],
    });
    const page = TestBed.runInInjectionContext(() => new MealPlannerPage());
    page.selectedDate.set(cacheDate);
    return page;
  }

  it('renders a matching cached day while refreshing and keeps identical fresh data in place', () => {
    localStorage.clear();
    seedCache();
    const response = new Subject<DailyMealPlan>();
    const getForDate = vi.fn(() => response.asObservable());
    const page = createPage(getForDate);

    page.loadPlan();

    const rendered = page.plan();
    const stored = localStorage.getItem(storageKey());
    expect(getForDate).toHaveBeenCalledWith(cacheDate);
    expect(rendered).toEqual(plan);
    expect(page.isLoadingPlan()).toBe(false);

    response.next({ ...plan, meals: { ...plan.meals, breakfast: [...plan.meals.breakfast] } });
    response.complete();

    expect(page.plan()).toBe(rendered);
    expect(localStorage.getItem(storageKey())).toBe(stored);
  });

  it('purges a cached week from another week before waiting for fresh data', () => {
    localStorage.clear();
    seedCache(plan, '2026-07-06');
    const response = new Subject<DailyMealPlan>();
    const page = createPage(() => response.asObservable());

    page.loadPlan();

    expect(localStorage.getItem(storageKey())).toBeNull();
    expect(page.plan()).toBeNull();
    expect(page.isLoadingPlan()).toBe(true);

    response.next(plan);
    response.complete();

    expect(page.plan()).toEqual(plan);
    expect(page.isLoadingPlan()).toBe(false);
  });

  it('updates the visible plan and cache after a different fresh response', () => {
    localStorage.clear();
    const response = new Subject<DailyMealPlan>();
    const page = createPage(() => response.asObservable());
    const fresh = { ...plan, meals: { ...plan.meals, breakfast: [] } };

    page.loadPlan();
    response.next(fresh);
    response.complete();

    expect(page.plan()).toEqual(fresh);
    expect(JSON.parse(localStorage.getItem(storageKey()) ?? '{}')).toMatchObject({
      userKey: 'user-1:house-1',
      weekStart: cacheDate,
      plans: { [cacheDate]: fresh },
    });
  });

  it('keeps a valid cached plan visible when refresh fails', () => {
    localStorage.clear();
    seedCache();
    const response = new Subject<DailyMealPlan>();
    const page = createPage(() => response.asObservable());
    page.loadPlan();
    const rendered = page.plan();

    response.error(new Error('offline'));

    expect(page.plan()).toBe(rendered);
    expect(page.planError()).toBe('meal.loadError');
    expect(page.isLoadingPlan()).toBe(false);
  });

  it('does not reuse another user or house cache', () => {
    localStorage.clear();
    seedCache();
    const response = new Subject<DailyMealPlan>();
    const page = createPage(() => response.asObservable(), { id: 'user-2', activeHouseId: 'house-2' });

    page.loadPlan();

    expect(page.plan()).toBeNull();
    expect(localStorage.getItem(storageKey())).not.toBeNull();
    expect(localStorage.getItem(storageKey({ id: 'user-2', activeHouseId: 'house-2' }))).toBeNull();

    response.next(plan);
    response.complete();
    expect(localStorage.getItem(storageKey({ id: 'user-2', activeHouseId: 'house-2' }))).not.toBeNull();
  });
});

function fakeInput(value: string): HTMLInputElement {
  const input = document.createElement('input');
  input.value = value;
  return input;
}

describe('MealPlannerPage portion validation', () => {
  function createPage(setParticipantPortion: MealPlannerService['setParticipantPortion'] = () => of(item)): MealPlannerPage {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: MealPlannerService,
          useValue: {
            getForDate: () => of(plan),
            getHouseMembers: () => of([]),
            getOverview: () => of([]),
            setParticipantPortion,
          },
        },
        { provide: RecipeService, useValue: { list: () => of({ items: [], total: 0 }), get: () => of(null) } },
        { provide: IngredientService, useValue: { getAll: () => of([ingredient]) } },
        { provide: ShoppingListIntegrationService, useValue: { addToShoppingList: () => of(null) } },
        { provide: TranslationService, useValue: { translate: (key: string) => key, language: () => 'en' } },
      ],
    });
    const fixture = TestBed.createComponent(MealPlannerPage);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('flags an out-of-range serving percentage as the user types', () => {
    const page = createPage();

    page.validateParticipantField(item, participant, 'portion', '500');
    expect(page.fieldError(item, participant, 'portion')).toBe('meal.rangeError');
    expect(page.itemFieldErrors(item)).toEqual(['meal.rangeError']);

    page.validateParticipantField(item, participant, 'portion', '120');
    expect(page.fieldError(item, participant, 'portion')).toBeNull();
    expect(page.itemFieldErrors(item)).toEqual([]);
  });

  it('does not flag an empty field while the user is mid-edit', () => {
    const page = createPage();

    page.validateParticipantField(item, participant, 'portion', '');
    expect(page.fieldError(item, participant, 'portion')).toBeNull();
  });

  it('clamps an out-of-range serving percentage on commit and resets the field', () => {
    let saved: number | undefined;
    const page = createPage((_id, _user, percent) => { saved = percent; return of(item); });

    const input = fakeInput('500');
    page.commitParticipantField(item, participant, 'portion', input);

    expect(saved).toBe(150);
    expect(input.value).toBe('150');
    expect(page.fieldError(item, participant, 'portion')).toBeNull();
  });

  it('clamps out-of-range calories back into the allowed range on commit', () => {
    let saved: number | undefined;
    const page = createPage((_id, _user, percent) => { saved = percent; return of(item); });

    // 500 kcal is far above the 1.5-serving ceiling (~116 kcal), so it clamps to 150%.
    const input = fakeInput('500');
    page.commitParticipantField(item, participant, 'calories', input);

    expect(saved).toBe(150);
    expect(input.value).toBe((caloriesPerServing * 1.5).toFixed(0));
  });

  it('reverts an empty field to the current portion without saving', () => {
    let called = false;
    const page = createPage(() => { called = true; return of(item); });

    const input = fakeInput('');
    page.commitParticipantField(item, participant, 'portion', input);

    expect(called).toBe(false);
    expect(input.value).toBe('100');
  });
});

describe('MealPlannerPage supplementary slots', () => {
  // The page reaches its day through the router, so the plan only lands once navigation settles.
  async function createPage(overview: MealPlanOverviewDay[], dailyPlan: DailyMealPlan = plan): Promise<MealPlannerPage> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: MealPlannerService,
          useValue: {
            getForDate: () => of(dailyPlan),
            getHouseMembers: () => of([]),
            getOverview: () => of(overview),
          },
        },
        { provide: RecipeService, useValue: { list: () => of({ items: [], total: 0 }), get: () => of(null) } },
        { provide: IngredientService, useValue: { getAll: () => of([ingredient]) } },
        { provide: ShoppingListIntegrationService, useValue: { addToShoppingList: () => of(null) } },
        { provide: TranslationService, useValue: { translate: (key: string) => key, language: () => 'en' } },
      ],
    });
    const fixture = TestBed.createComponent(MealPlannerPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('drops the second breakfast and snack rows when nothing in the range uses them', async () => {
    const page = await createPage([{ date: '2026-07-13', plannedSlots: ['breakfast', 'supper'] }]);

    expect(page.overviewSlots()).toEqual(['breakfast', 'dinner', 'supper']);
  });

  it('keeps an optional row as soon as one day in the range plans it', async () => {
    const page = await createPage([
      { date: '2026-07-13', plannedSlots: ['breakfast'] },
      { date: '2026-07-14', plannedSlots: ['snack'] },
    ]);

    expect(page.overviewSlots()).toEqual(['breakfast', 'dinner', 'snack', 'supper']);
  });

  it('collapses only the empty optional slots of the shown day', async () => {
    const page = await createPage([]);

    expect(page.isSlotCollapsed('second-breakfast')).toBe(true);
    expect(page.isSlotCollapsed('snack')).toBe(true);
    expect(page.isSlotCollapsed('breakfast')).toBe(false);
  });

  it('expands an optional slot the user opens, and one that already holds a meal', async () => {
    const page = await createPage([], { ...plan, meals: { ...emptyMeals(), snack: [{ ...item, slot: 'snack' }] } });

    page.openSlot('second-breakfast');

    expect(page.isSlotCollapsed('second-breakfast')).toBe(false);
    expect(page.isSlotCollapsed('snack')).toBe(false);
  });
});

describe('isValidDateString', () => {
  it.each(['2026-07-16', '2028-02-29'])('accepts %s', (date) => expect(isValidDateString(date)).toBe(true));
  it.each([null, '', '16-07-2026', '2026-13-01', '2026-02-30'])('rejects %s', (date) =>
    expect(isValidDateString(date)).toBe(false));
});
