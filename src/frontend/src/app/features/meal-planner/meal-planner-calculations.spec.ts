import { describe, expect, it } from 'vitest';
import { Ingredient } from '../ingredients/ingredient.models';
import { RecipeDetail } from '../recipes/models/recipe.models';
import { MealPlanItem } from './models/meal-planner.models';
import {
  allocateCaloriesByPerson,
  directIngredientCaloriesPerServing,
  directIngredientQuantity,
  recipeCaloriesPerServing,
  recipePricePerServing,
  scaleRecipeQuantity,
} from './meal-planner-calculations';

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
  id: 'recipe',
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

describe('meal planner calculations', () => {
  it('uses one piece per serving for a countable direct ingredient', () => {
    const countable = { ...ingredient, isCountable: true, measurementUnitsPerPiece: 150, caloriesPer100BaseUnits: 52 };

    expect(directIngredientQuantity(countable, 3)).toBe(450);
    expect(directIngredientQuantity(countable, 0)).toBe(0);
    expect(directIngredientCaloriesPerServing(countable)).toBe(78);
  });

  it('uses 100 base units per serving for a direct measured ingredient', () => {
    expect(directIngredientQuantity(ingredient, 1.5)).toBe(150);
  });

  it('calculates recipe price for one serving from the recipe yield', () => {
    expect(recipePricePerServing(recipe, [ingredient])).toBe(5);
  });

  it('calculates recipe calories for one serving from normalized quantities', () => {
    expect(recipeCaloriesPerServing(recipe, [{ ...ingredient, caloriesPer100BaseUnits: 350 }])).toBe(350);
  });

  it('scales a recipe ingredient from its batch yield to the people served', () => {
    expect(scaleRecipeQuantity(400, 4, 3)).toBe(300);
  });

  it('allocates calories by person, guests, and unassigned servings', () => {
    const alice = { userId: 'alice', displayName: 'Alice', isCurrentUser: false, portionPercent: 150 };
    const bob = { userId: 'bob', displayName: 'Bob', isCurrentUser: false, portionPercent: 50 };

    const items: MealPlanItem[] = [
      {
        id: '1',
        slot: 'breakfast',
        type: 'recipe',
        recipeId: recipe.id,
        displayName: 'Breakfast',
        sortOrder: 0,
        participants: [alice, bob],
        guests: 1,
        servings: 4,
        servingsOverridden: false,
      },
      {
        id: '2',
        slot: 'dinner',
        type: 'ingredient',
        ingredientId: ingredient.id,
        displayName: 'Side',
        sortOrder: 0,
        participants: [alice],
        guests: 0,
        servings: 2,
        servingsOverridden: false,
      },
      {
        id: '3',
        slot: 'snack',
        type: 'ingredient',
        ingredientId: ingredient.id,
        displayName: 'Unassigned snack',
        sortOrder: 0,
        participants: [],
        guests: 0,
        servings: 3,
        servingsOverridden: false,
      },
    ];

    const recipeWithHighCalories = {
      ...recipe,
      ingredients: [{ ...recipe.ingredients[0], normalizedQuantity: 400 }],
    };

    const calorieIngredient = { ...ingredient, caloriesPer100BaseUnits: 100 };

    const result = allocateCaloriesByPerson(
      items,
      { [recipe.id]: recipeWithHighCalories },
      [calorieIngredient],
      'Guests',
      'Unassigned',
    );

    expect(result).toHaveLength(4);
    expect(result.find((person) => person.id === 'alice')!.total).toBe(300);
    expect(result.find((person) => person.id === 'alice')!.meals.breakfast).toBe(150);
    expect(result.find((person) => person.id === 'bob')!.total).toBe(50);
    expect(result.find((person) => person.id === 'guests')!.total).toBe(100);
    expect(result.find((person) => person.id === 'unassigned')!.total).toBe(300);
  });
});
