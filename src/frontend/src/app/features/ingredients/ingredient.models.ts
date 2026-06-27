export interface Ingredient {
  id: string;
  name: string;
  measurementUnit: string;
  caloriesPer100Grams: number;
  price: number;
}

export type IngredientRequest = Omit<Ingredient, 'id'>;

export const measurementUnits = [
  { label: 'Grams (g)', value: 'g' },
  { label: 'Kilograms (kg)', value: 'kg' },
  { label: 'Millilitres (ml)', value: 'ml' },
  { label: 'Litres (l)', value: 'l' },
  { label: 'Pieces', value: 'piece' },
  { label: 'Teaspoons (tsp)', value: 'tsp' },
  { label: 'Tablespoons (tbsp)', value: 'tbsp' },
  { label: 'Cups', value: 'cup' },
] as const;
