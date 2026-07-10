import { describe, expect, it } from 'vitest';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { aggregateIngredientProperties } from './recipe-detail-page';

const ingredient = (id: string, allergens: number, attributes: number, suitability: number) =>
  ({ id, allergens, attributes, suitability }) as Ingredient;

describe('aggregateIngredientProperties', () => {
  it('deduplicates recipe ingredients, unions warnings, and intersects suitable diets', () => {
    const result = aggregateIngredientProperties(
      ['flour', 'milk', 'flour'],
      [ingredient('flour', 1, 2, 11), ingredient('milk', 64, 4, 2), ingredient('unused', 8, 8, 8)],
    );

    expect(result).toEqual({ allergens: 65, attributes: 6, suitability: 2 });
  });
});
