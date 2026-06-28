export interface Ingredient {
  id: string;
  /** The name resolved for the current language (translation, falling back to the default name). */
  name: string;
  /** The canonical (default-language) name stored on the ingredient. */
  defaultName: string;
  /** The translation for the current language, or null when none exists / the language is default. */
  translation: string | null;
  measurementUnit: string;
  isCountable: boolean;
  measurementUnitsPerPiece: number | null;
  caloriesPer100BaseUnits: number;
  pricePer100BaseUnits: number;
  svgIcon: string | null;
}

export interface IngredientRequest {
  /** The canonical (default-language) name. */
  name: string;
  /** The translation for the current language. Omit when editing in the default language. */
  translation?: string | null;
  measurementUnit: string;
  isCountable: boolean;
  measurementUnitsPerPiece: number | null;
  caloriesPer100BaseUnits: number;
  pricePer100BaseUnits: number;
}

export const measurementUnits = [
  { label: 'Grams (g)', value: 'g' },
  { label: 'Millilitres (ml)', value: 'ml' },
] as const;
