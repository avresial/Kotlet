import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { Ingredient } from '../../ingredients/ingredient.models';
import { RecipeDetail } from '../../recipes/models/recipe.models';
import { ShoppingListService } from '../../shopping-list/shopping-list.service';
import { ShoppingListItem } from '../../shopping-list/shopping-list.models';
import { MealPlanItem } from '../models/meal-planner.models';
import { ShoppingListIntegrationService } from './shopping-list-integration.service';

const ingredient: Ingredient = {
  id: 'pasta',
  name: 'Pasta',
  defaultName: 'Pasta',
  translation: null,
  measurementUnit: 'g',
  isCountable: false,
  measurementUnitsPerPiece: null,
  caloriesPer100BaseUnits: 0,
  pricePer100BaseUnits: 5,
  svgIcon: null,
  category: 0,
  allergens: 0,
  attributes: 0,
  suitability: 0,
  isAiModified: false,
  createdAtUtc: '2026-01-01T00:00:00Z',
};

const recipe: RecipeDetail = {
  id: 'pasta-recipe',
  title: 'Pasta dinner',
  slug: 'pasta-dinner',
  createdByUserId: 'user',
  descriptionMarkdown: null,
  servings: 4,
  mealType: null,
  ingredients: [{
    id: 'line',
    sortOrder: 0,
    ingredientId: ingredient.id,
    name: ingredient.name,
    quantity: 400,
    unit: 'g',
    normalizedQuantity: 400,
    normalizedUnit: 'g',
    note: null,
  }],
  images: [],
  canEdit: true,
  isAiAssisted: false,
  sourceUrl: null,
  createdAtUtc: '2026-06-28T00:00:00Z',
  updatedAtUtc: '2026-06-28T00:00:00Z',
};

describe('ShoppingListIntegrationService', () => {
  let service: ShoppingListIntegrationService;
  let shoppingListService: ShoppingListService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ShoppingListIntegrationService,
        { provide: ShoppingListService, useValue: { getAll: vi.fn(), create: vi.fn(), update: vi.fn() } },
      ],
    });
    service = TestBed.inject(ShoppingListIntegrationService);
    shoppingListService = TestBed.inject(ShoppingListService);
  });

  it('merges scaled recipe ingredients with existing shopping list entries', async () => {
    const item: MealPlanItem = {
      id: 'meal-item-1',
      slot: 'dinner',
      type: 'recipe',
      recipeId: recipe.id,
      displayName: recipe.title,
      sortOrder: 0,
      participants: [],
      guests: 0,
      servings: 2,
      servingsOverridden: false,
    };

    const existing: ShoppingListItem = {
      id: 'entry-existing',
      ingredientId: ingredient.id,
      ingredientName: ingredient.name,
      measurementUnit: ingredient.measurementUnit,
      quantity: 100,
      pricePer100BaseUnits: ingredient.pricePer100BaseUnits,
      totalPrice: 5,
      isPurchased: false,
      category: ingredient.category,
    };

    const updated: ShoppingListItem = {
      id: 'entry-existing',
      ingredientId: ingredient.id,
      ingredientName: ingredient.name,
      measurementUnit: ingredient.measurementUnit,
      quantity: 300,
      pricePer100BaseUnits: ingredient.pricePer100BaseUnits,
      totalPrice: 15,
      isPurchased: false,
      category: ingredient.category,
    };

    vi.mocked(shoppingListService.getAll).mockReturnValue(of([existing]));
    vi.mocked(shoppingListService.update).mockReturnValue(of(updated));

    const result = await firstValueFrom(
      service.addToShoppingList(item, { [recipe.id]: recipe }, [ingredient], 'No catalogue ingredients')
    );

    expect(result).toHaveLength(1);
    expect(result[0]).toEqual(updated);
    expect(shoppingListService.update).toHaveBeenCalledWith(existing, {
      quantity: 300,
      isPurchased: false,
    });
  });
});
