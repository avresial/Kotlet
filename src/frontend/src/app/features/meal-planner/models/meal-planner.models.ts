export type MealSlot = 'breakfast' | 'dinner' | 'supper';
export type MealPlanItemType = 'recipe' | 'ingredient';

export interface MealParticipant {
  userId: string;
  displayName: string;
  isCurrentUser: boolean;
}

export interface MealPlanItem {
  id: string;
  slot: MealSlot;
  type: MealPlanItemType;
  recipeId?: string | null;
  ingredientId?: string | null;
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
  note?: string | null;
}

export interface HouseMember {
  userId: string;
  displayName: string;
}

export interface SetParticipantsRequest {
  userIds: string[];
}

export interface SetServingsRequest {
  servings: number | null;
}

export interface SetGuestsRequest {
  guests: number;
}
