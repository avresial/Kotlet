import { describe, expect, it } from 'vitest';
import { Ingredient } from './ingredient.models';
import { displayMeasurement, fromBasePer100, toBasePer100, toBaseQuantity } from './display-units';

const ingredient = (measurementUnit: 'g' | 'ml', pieceSize: number | null = null): Ingredient => ({
  id: '1', name: 'Test', defaultName: 'Test', translation: null, measurementUnit,
  isCountable: pieceSize !== null, measurementUnitsPerPiece: pieceSize,
  caloriesPer100BaseUnits: 0, pricePer100BaseUnits: 0, svgIcon: null, category: 0,
});

describe('display units', () => {
  it('converts mass, volume, and pieces to base quantities', () => {
    expect(toBaseQuantity(1.5, 'kg', ingredient('g'))).toBe(1500);
    expect(toBaseQuantity(2, 'l', ingredient('ml'))).toBe(2000);
    expect(toBaseQuantity(3, 'piece', ingredient('g', 125))).toBe(375);
  });

  it('preserves cost and calories through display-basis conversion', () => {
    expect(toBasePer100(25, 'kg', null)).toBe(2.5);
    expect(fromBasePer100(2.5, 'kg', null)).toBe(25);
    expect(toBasePer100(3, 'piece', 150)).toBe(2);
  });

  it('chooses pieces first, then large units, then base units', () => {
    expect(displayMeasurement(300, ingredient('g', 150))).toEqual({ quantity: 2, unit: 'piece' });
    expect(displayMeasurement(1500, ingredient('g'))).toEqual({ quantity: 1.5, unit: 'kg' });
  });
});
