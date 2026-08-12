export enum VideoTranscriptionStatus {
  Pending,
  Transcribing,
  DetectingIngredients,
  MatchingIngredients,
  Ready,
  Failed,
}

export type IngredientConfidenceState = 'confident' | 'uncertain' | 'new';

export interface DetectedIngredient {
  sourceName: string;
  quantity: number | null;
  unit: string | null;
  note: string | null;
  matchedIngredientId: string | null;
  matchedIngredientName: string | null;
  matchScore: number | null;
  isProposedNew: boolean;
}

export interface VideoTranscriptionJob {
  id: string;
  status: VideoTranscriptionStatus;
  transcript: string | null;
  title: string | null;
  author: string | null;
  platform: string | null;
  language: string | null;
  sourceUrl: string;
  detectedIngredients: DetectedIngredient[];
  errorReason: string | null;
  recipeImportJobId: string | null;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface CreateVideoTranscriptionResponse {
  id: string;
}

export interface ContinueAsRecipeResponse {
  id: string;
}
