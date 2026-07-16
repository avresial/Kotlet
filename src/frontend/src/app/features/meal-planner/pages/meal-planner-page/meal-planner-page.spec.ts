import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { RecipeService } from '../../../recipes/services/recipe.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { DailyMealPlan, MealParticipant, MealPlanItem } from '../../models/meal-planner.models';
import { MealPlannerService } from '../../services/meal-planner.service';
import { ShoppingListIntegrationService } from '../../services/shopping-list-integration.service';
import { isValidDateString, MealPlannerPage, weekStart } from './meal-planner-page';

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

describe('isValidDateString', () => {
  it.each(['2026-07-16', '2028-02-29'])('accepts %s', (date) => expect(isValidDateString(date)).toBe(true));
  it.each([null, '', '16-07-2026', '2026-13-01', '2026-02-30'])('rejects %s', (date) =>
    expect(isValidDateString(date)).toBe(false));
});
