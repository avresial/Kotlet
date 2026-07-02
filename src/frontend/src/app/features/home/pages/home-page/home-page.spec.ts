import { describe, expect, it } from 'vitest';
import { RecipeDetail } from '../../../recipes/models/recipe.models';
import { ingredientPreview } from './home-page';

describe('ingredientPreview', () => {
  it('shows at most three ingredient names', () => {
    const recipe = { ingredients: ['Tomato', 'Garlic', 'Cream', 'Salt'].map(name => ({ name })) } as RecipeDetail;
    expect(ingredientPreview(recipe)).toBe('Tomato, Garlic, Cream');
  });
});
