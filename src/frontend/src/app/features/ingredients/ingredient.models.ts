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
  allergens: number;
  attributes: number;
  suitability: number;
  isAiModified: boolean;
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
  allergens: number;
  attributes: number;
  suitability: number;
  isAiModified?: boolean;
}

export interface IngredientDetailsSuggestion { category: number; allergens: number; attributes: number; suitability: number; }

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

/** Mirrors the persisted schema in Kotlet.Domain.Ingredients.Allergen - bit values must stay in sync. */
export const allergenOptions = [
  { label: 'allergen.gluten', value: 1 << 0 },
  { label: 'allergen.crustaceans', value: 1 << 1 },
  { label: 'allergen.eggs', value: 1 << 2 },
  { label: 'allergen.fish', value: 1 << 3 },
  { label: 'allergen.peanuts', value: 1 << 4 },
  { label: 'allergen.soybeans', value: 1 << 5 },
  { label: 'allergen.milk', value: 1 << 6 },
  { label: 'allergen.treeNuts', value: 1 << 7 },
  { label: 'allergen.celery', value: 1 << 8 },
  { label: 'allergen.mustard', value: 1 << 9 },
  { label: 'allergen.sesame', value: 1 << 10 },
  { label: 'allergen.sulphites', value: 1 << 11 },
  { label: 'allergen.lupin', value: 1 << 12 },
  { label: 'allergen.molluscs', value: 1 << 13 },
] as const;

/** Mirrors the persisted schema in Kotlet.Domain.Ingredients.FoodAttribute - bit values must stay in sync. */
export const foodAttributeOptions = [
  { label: 'foodAttribute.animalOrigin', value: 1 << 0 },
  { label: 'foodAttribute.plantOrigin', value: 1 << 1 },
  { label: 'foodAttribute.containsLactose', value: 1 << 2 },
  { label: 'foodAttribute.containsAlcohol', value: 1 << 3 },
  { label: 'foodAttribute.containsCaffeine', value: 1 << 4 },
  { label: 'foodAttribute.highHistamine', value: 1 << 5 },
  { label: 'foodAttribute.highFodmap', value: 1 << 6 },
  { label: 'foodAttribute.fermented', value: 1 << 7 },
  { label: 'foodAttribute.smoked', value: 1 << 8 },
  { label: 'foodAttribute.spicy', value: 1 << 9 },
  { label: 'foodAttribute.processed', value: 1 << 10 },
  { label: 'foodAttribute.ultraProcessed', value: 1 << 11 },
] as const;

/** Mirrors the persisted schema in Kotlet.Domain.Ingredients.DietarySuitability - bit values must stay in sync. */
export const dietarySuitabilityOptions = [
  { label: 'dietarySuitability.vegan', value: 1 << 0 },
  { label: 'dietarySuitability.vegetarian', value: 1 << 1 },
  { label: 'dietarySuitability.pescatarian', value: 1 << 2 },
  { label: 'dietarySuitability.glutenFree', value: 1 << 3 },
  { label: 'dietarySuitability.lactoseFree', value: 1 << 4 },
  { label: 'dietarySuitability.lowFodmap', value: 1 << 5 },
  { label: 'dietarySuitability.lowHistamine', value: 1 << 6 },
  { label: 'dietarySuitability.keto', value: 1 << 7 },
  { label: 'dietarySuitability.lowCarb', value: 1 << 8 },
] as const;
