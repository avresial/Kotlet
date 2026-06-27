export type MealSlot = 'breakfast' | 'dinner' | 'supper';
export type MealPlanItemType = 'recipe' | 'ingredient';

export interface MealPlanItem {
  id: string;
  slot: MealSlot;
  type: MealPlanItemType;
  recipeId?: string | null;
  ingredientId?: string | null;
  displayName: string;
  note?: string | null;
  sortOrder: number;
}

export interface DailyMealPlan {
  date: string;
  meals: Record<MealSlot, MealPlanItem[]>;
}

export interface AddMealPlanItemRequest {
  date: string;
  slot: MealSlot;
  recipeId?: string | null;
  ingredientId?: string | null;
  note?: string | null;
}
