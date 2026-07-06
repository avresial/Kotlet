import { describe, expect, it } from 'vitest';
import { Ingredient } from '../../ingredient.models';
import { filterIngredients, paginate } from './ingredients-page';

const ingredient = (name: string, category: number): Ingredient => ({
  id: name, name, defaultName: name, translation: null, measurementUnit: 'g', isCountable: false,
  measurementUnitsPerPiece: null, caloriesPer100BaseUnits: 0, pricePer100BaseUnits: 0,
  svgIcon: null, category, allergens: 0, attributes: 0, suitability: 0, isAiModified: false, createdAtUtc: '2026-01-01T00:00:00Z',
});

describe('filterIngredients', () => {
  it('combines name and category filters and clears with null', () => {
    const items = [ingredient('Green apple', 21), ingredient('Apple pie', 90), ingredient('Pear', 21)];

    expect(filterIngredients(items, 'apple', 21).map(item => item.name)).toEqual(['Green apple']);
    expect(filterIngredients(items, '', null)).toEqual(items);
  });
});

describe('paginate', () => {
  it('slices the requested page and returns a short last page', () => {
    const items = ['a', 'b', 'c', 'd', 'e'];

    expect(paginate(items, 1, 2)).toEqual(['a', 'b']);
    expect(paginate(items, 3, 2)).toEqual(['e']);
    expect(paginate(items, 4, 2)).toEqual([]);
  });
});
