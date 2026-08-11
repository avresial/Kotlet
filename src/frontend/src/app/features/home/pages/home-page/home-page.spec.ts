import { HttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
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
import { HomeService } from '../../home.service';
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
        { provide: HttpClient, useValue: {} },
        { provide: PantryService, useValue: {} },
        { provide: IngredientService, useValue: {} },
        { provide: ShoppingListService, useValue: {} },
        { provide: RecipeService, useValue: {} },
        { provide: MealPlannerService, useValue: { getForDate } },
        { provide: HomeService, useValue: {} },
        {
          provide: TranslationService,
          useValue: { language: signal('en'), translate: (key: string) => key },
        },
      ],
    });
    page = TestBed.runInInjectionContext(() => new HomePage());
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

  it('calculates offsets by calendar date across daylight-saving changes', () => {
    expect(localDayOffset('2026-03-28', '2026-03-30')).toBe(2);
    expect(addLocalDays('2026-03-29', 1)).toBe('2026-03-30');
  });
});
