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

export type RecipeAuditImportance = 'important' | 'minor';
export type RecipeAuditElement = 'ingredients' | 'description' | 'image' | 'mealType';

export interface RecipeAuditItem {
  id: string;
  title: string;
  slug: string;
  importance: RecipeAuditImportance;
  missingElements: RecipeAuditElement[];
  missingCount: number;
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
  canEdit: boolean;
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
  /** Attribution for the image's primary source; null when the image has no recorded source. */
  source?: ImageSourceAttribution | null;
}

export interface RecipeImageCandidate {
  provider: string;
  externalImageId: string;
  previewUrl: string;
  sourcePageUrl: string;
  authorName: string | null;
  authorUrl: string | null;
  altText: string | null;
  width: number | null;
  height: number | null;
}

export interface RecipeImageImportResult {
  content: string;
  contentType: 'image/webp';
  width: number;
  height: number;
  provider: string;
  externalImageId: string;
  sourcePageUrl: string;
  authorName: string | null;
  authorUrl: string | null;
  altText: string | null;
}

export interface RecipeImageSourceData {
  provider: string;
  externalId: string | null;
  url: string | null;
  authorName: string | null;
  authorUrl: string | null;
}

export interface ImageSourceAttribution {
  provider: string;
  authorName: string | null;
  authorUrl: string | null;
  url: string | null;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export enum RecipeImportStatus {
  Pending,
  FetchingTranscript,
  Extracting,
  ResolvingIngredients,
  ReadyForReview,
  Failed,
}

export interface RecipeImportIngredient {
  name: string;
  quantity: number | null;
  unit: string | null;
  note: string | null;
  ingredientId: string | null;
  matchedName: string | null;
  isProposedNew: boolean;
}

export interface RecipeImportDraft {
  title: string;
  servings: number;
  instructionsMarkdown: string;
  gaps: string[];
  ingredients: RecipeImportIngredient[];
  duplicateMatches: RecipeExistenceMatch[];
}

export interface RecipeExistenceMatch {
  recipeId: string;
  title: string;
  sourceUrl: string | null;
  matchType: number;
}

export interface RecipeImportJob {
  id: string;
  status: RecipeImportStatus;
  draft: RecipeImportDraft | null;
  errorReason: string | null;
}
