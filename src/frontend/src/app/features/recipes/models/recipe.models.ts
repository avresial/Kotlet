export interface RecipeIngredientRequest {
  ingredientId: string;
  name: string;
  quantity: number;
  unit: string;
  note: string | null;
}

export interface CreateRecipeRequest {
  title: string;
  descriptionMarkdown: string | null;
  /** Number of adult portions the recipe yields. One serving is a single adult portion. */
  servings: number;
  mealType?: RecipeMealType | null;
  ingredients: RecipeIngredientRequest[];
  /** Original source of the recipe (e.g. the video or web page it was imported from). */
  sourceUrl?: string | null;
  /** Marks the recipe as created with AI assistance; ignored on update (provenance is kept). */
  isAiAssisted?: boolean;
}

export type RecipeMealType = 'breakfast' | 'second-breakfast' | 'dinner' | 'snack' | 'supper';
export const recipeMealTypes: { value: RecipeMealType; label: string }[] = [
  { value: 'breakfast', label: 'meal.slot.breakfast' },
  { value: 'second-breakfast', label: 'meal.slot.second-breakfast' },
  { value: 'dinner', label: 'meal.slot.dinner' },
  { value: 'snack', label: 'meal.slot.snack' },
  { value: 'supper', label: 'meal.slot.supper' },
];

export type UpdateRecipeRequest = CreateRecipeRequest;

export interface RecipeIngredient {
  id: string;
  sortOrder: number;
  ingredientId: string;
  name: string;
  quantity: number;
  unit: string;
  normalizedQuantity: number;
  normalizedUnit: string;
  note: string | null;
}

export interface RecipeSummary {
  id: string;
  title: string;
  slug: string;
  /** User id of the household member who created the recipe. */
  createdByUserId: string;
  ingredientCount: number;
  servings: number;
  mealType: RecipeMealType | null;
  firstImageUrl: string | null;
  isAiAssisted: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface RecipeDetail {
  id: string;
  title: string;
  slug: string;
  /** User id of the household member who created the recipe. */
  createdByUserId: string;
  descriptionMarkdown: string | null;
  servings: number;
  mealType: RecipeMealType | null;
  ingredients: RecipeIngredient[];
  images: RecipeImage[];
  isAiAssisted: boolean;
  sourceUrl: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface RecipeImage {
  id: string;
  recipeId: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  altText: string | null;
  sortOrder: number;
  contentUrl: `/api/${string}`;
  createdAtUtc: string;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
