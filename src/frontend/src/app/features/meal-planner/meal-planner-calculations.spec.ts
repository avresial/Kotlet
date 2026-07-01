import { describe, expect, it } from 'vitest';
import { Ingredient } from '../ingredients/ingredient.models';
import { RecipeDetail } from '../recipes/models/recipe.models';
import {
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
};

const recipe: RecipeDetail = {
  id: 'recipe',
  title: 'Pasta dinner',
  slug: 'pasta-dinner',
  descriptionMarkdown: null,
  servings: 4,
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
  createdAtUtc: '2026-06-28T00:00:00Z',
  updatedAtUtc: '2026-06-28T00:00:00Z',
};

describe('meal planner calculations', () => {
  it('uses one piece per serving for a countable direct ingredient', () => {
    const countable = { ...ingredient, isCountable: true, measurementUnitsPerPiece: 150, caloriesPer100BaseUnits: 52 };

    expect(directIngredientQuantity(countable, 3)).toBe(450);
    expect(directIngredientQuantity(countable, 0)).toBe(150);
    expect(directIngredientCaloriesPerServing(countable)).toBe(78);
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
});
