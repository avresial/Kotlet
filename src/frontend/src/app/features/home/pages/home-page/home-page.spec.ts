import { describe, expect, it } from 'vitest';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { RecipeDetail } from '../../../recipes/models/recipe.models';
import { ingredientPreview, newestIngredients } from './home-page';

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
