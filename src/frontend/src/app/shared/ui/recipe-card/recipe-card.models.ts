export interface RecipeCardModel {
  id: string;
  title: string;
  mealType?: string | null;
  mealTypeLabel?: string | null;
  servings?: number;
  servingsLabel?: string | null;
  ingredientCount?: number;
  ingredientCountLabel?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  isAiAssisted?: boolean;
  viewLabel?: string;
}

export interface AgentStructuredResult {
  type: 'recipes';
  recipes: RecipeCardModel[];
  totalCount: number;
}
