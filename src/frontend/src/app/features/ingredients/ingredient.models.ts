export interface Ingredient {
  id: string;
  name: string;
  measurementUnit: string;
  isCountable: boolean;
  measurementUnitsPerPiece: number | null;
  caloriesPer100BaseUnits: number;
  pricePer100BaseUnits: number;
}

export type IngredientRequest = Omit<Ingredient, 'id'>;

export const measurementUnits = [
  { label: 'Grams (g)', value: 'g' },
  { label: 'Millilitres (ml)', value: 'ml' },
] as const;
