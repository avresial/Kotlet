export interface PantryItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  measurementUnit: string;
  quantity: number;
  expirationDate: string | null;
  storageLocation: 1 | 2 | 3 | null;
}

export interface PantryRecipeMatchIngredient {
  ingredientId: string;
  name: string;
}

export interface PantryRecipeMatch {
  recipeId: string;
  title: string;
  slug: string;
  totalIngredientCount: number;
  matchedIngredientCount: number;
  isFullMatch: boolean;
  missingIngredients: PantryRecipeMatchIngredient[];
}

export const storageLocations = [
  { value: 1, label: 'pantry.location.refrigerator' },
  { value: 2, label: 'pantry.location.freezer' },
  { value: 3, label: 'pantry.location.cabinet' },
] as const;
