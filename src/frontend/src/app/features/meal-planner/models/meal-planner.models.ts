export type MealSlot = 'breakfast' | 'second-breakfast' | 'dinner' | 'snack' | 'supper';
export type MealPlanItemType = 'recipe' | 'ingredient' | 'prepared-meal';

export interface MealParticipant {
  userId: string;
  displayName: string;
  isCurrentUser: boolean;
  portionPercent: number;
}

export interface MealPlanItem {
  id: string;
  slot: MealSlot;
  type: MealPlanItemType;
  recipeId?: string | null;
  ingredientId?: string | null;
  preparedMealId?: string | null;
  parentMealPlanItemId?: string | null;
  ingredientQuantity?: number | null;
  ingredientUnit?: string | null;
  displayName: string;
  note?: string | null;
  sortOrder: number;
  participants: MealParticipant[];
  guests: number;
  servings: number;
  servingsOverridden: boolean;
}

export interface DailyMealPlan {
  date: string;
  meals: Record<MealSlot, MealPlanItem[]>;
}

export interface MealPlanOverviewDay {
  date: string;
  plannedSlots: MealSlot[];
}

export interface AddMealPlanItemRequest {
  date: string;
  slot: MealSlot;
  recipeId?: string | null;
  ingredientId?: string | null;
  preparedMealId?: string | null;
  addons?: { ingredientId: string; quantity: number; unit: string }[];
  note?: string | null;
}

export interface HouseMember {
  userId: string;
  displayName: string;
}
