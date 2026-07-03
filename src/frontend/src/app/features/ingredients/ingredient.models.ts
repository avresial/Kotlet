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
  category: number;
  createdAtUtc: string;
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
  category: number;
}

export const measurementUnits = [
  { label: 'units.grams', value: 'g' },
  { label: 'units.millilitres', value: 'ml' },
] as const;

/** Mirrors the persisted schema in Kotlet.Domain.Ingredients.FoodCategory - values must stay in sync. */
export const foodCategories = [
  { label: 'foodCategory.unknown', value: 0 },
  { label: 'foodCategory.meat', value: 1 },
  { label: 'foodCategory.poultry', value: 2 },
  { label: 'foodCategory.fish', value: 3 },
  { label: 'foodCategory.shellfish', value: 4 },
  { label: 'foodCategory.egg', value: 5 },
  { label: 'foodCategory.dairy', value: 6 },
  { label: 'foodCategory.cheese', value: 7 },
  { label: 'foodCategory.vegetable', value: 20 },
  { label: 'foodCategory.fruit', value: 21 },
  { label: 'foodCategory.legume', value: 22 },
  { label: 'foodCategory.grain', value: 23 },
  { label: 'foodCategory.nut', value: 24 },
  { label: 'foodCategory.seed', value: 25 },
  { label: 'foodCategory.mushroom', value: 26 },
  { label: 'foodCategory.herb', value: 40 },
  { label: 'foodCategory.spice', value: 41 },
  { label: 'foodCategory.oil', value: 60 },
  { label: 'foodCategory.sweetener', value: 61 },
  { label: 'foodCategory.condiment', value: 62 },
  { label: 'foodCategory.sauce', value: 63 },
  { label: 'foodCategory.beverage', value: 64 },
  { label: 'foodCategory.composite', value: 90 },
  { label: 'foodCategory.additive', value: 91 },
] as const;
