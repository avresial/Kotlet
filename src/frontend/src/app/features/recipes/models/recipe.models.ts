export interface RecipeIngredientRequest {
  name: string;
  quantity: number | null;
  unit: string | null;
  note: string | null;
}

export interface CreateRecipeRequest {
  title: string;
  descriptionMarkdown: string | null;
  ingredients: RecipeIngredientRequest[];
}

export type UpdateRecipeRequest = CreateRecipeRequest;

export interface RecipeIngredient {
  id: string;
  sortOrder: number;
  name: string;
  quantity: number | null;
  unit: string | null;
  note: string | null;
}

export interface RecipeSummary {
  id: string;
  title: string;
  slug: string;
  ingredientCount: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface RecipeDetail {
  id: string;
  title: string;
  slug: string;
  descriptionMarkdown: string | null;
  ingredients: RecipeIngredient[];
  images: RecipeImage[];
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
