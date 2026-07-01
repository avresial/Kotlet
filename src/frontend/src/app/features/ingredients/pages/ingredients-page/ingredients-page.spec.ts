import { describe, expect, it } from 'vitest';
import { Ingredient } from '../../ingredient.models';
import { filterIngredients } from './ingredients-page';

const ingredient = (name: string, category: number): Ingredient => ({
  id: name, name, defaultName: name, translation: null, measurementUnit: 'g', isCountable: false,
  measurementUnitsPerPiece: null, caloriesPer100BaseUnits: 0, pricePer100BaseUnits: 0,
  svgIcon: null, category,
});

describe('filterIngredients', () => {
  it('combines name and category filters and clears with null', () => {
    const items = [ingredient('Green apple', 21), ingredient('Apple pie', 90), ingredient('Pear', 21)];

    expect(filterIngredients(items, 'apple', 21).map(item => item.name)).toEqual(['Green apple']);
    expect(filterIngredients(items, '', null)).toEqual(items);
  });
});
