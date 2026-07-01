import { Ingredient } from './ingredient.models';

export type DisplayUnit = 'g' | 'kg' | 'ml' | 'l' | 'piece';

export const displayUnitOptions = [
  { value: 'g', label: 'units.grams' },
  { value: 'kg', label: 'units.kilograms' },
  { value: 'ml', label: 'units.millilitres' },
  { value: 'l', label: 'units.litres' },
  { value: 'piece', label: 'units.piece' },
] as const;

export function unitsForIngredient(ingredient: Ingredient): DisplayUnit[] {
  return ingredient.measurementUnit === 'g'
    ? ['g', 'kg', ...(ingredient.isCountable ? ['piece' as const] : [])]
    : ['ml', 'l', ...(ingredient.isCountable ? ['piece' as const] : [])];
}

export function toBaseQuantity(quantity: number, unit: DisplayUnit, ingredient: Ingredient): number {
  if (unit === 'kg' || unit === 'l') return quantity * 1000;
  if (unit === 'piece') return quantity * (ingredient.measurementUnitsPerPiece ?? 0);
  return quantity;
}

export function displayMeasurement(quantity: number, ingredient: Ingredient): { quantity: number; unit: DisplayUnit } {
  const pieceSize = ingredient.measurementUnitsPerPiece;
  if (ingredient.isCountable && pieceSize && quantity % pieceSize === 0)
    return { quantity: quantity / pieceSize, unit: 'piece' };
  if (quantity >= 1000)
    return { quantity: quantity / 1000, unit: ingredient.measurementUnit === 'g' ? 'kg' : 'l' };
  return { quantity, unit: ingredient.measurementUnit as 'g' | 'ml' };
}

export function toBasePer100(value: number, unit: DisplayUnit, pieceSize: number | null): number {
  const basis = unit === 'kg' || unit === 'l' ? 1000 : unit === 'piece' ? pieceSize ?? 0 : 100;
  return basis ? value * 100 / basis : 0;
}

export function fromBasePer100(value: number, unit: DisplayUnit, pieceSize: number | null): number {
  const basis = unit === 'kg' || unit === 'l' ? 1000 : unit === 'piece' ? pieceSize ?? 0 : 100;
  return value * basis / 100;
}
