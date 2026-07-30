export interface ShoppingListItem {
  id: string;
  ingredientId?: string | null;
  preparedMealId?: string | null;
  ingredientName: string;
  measurementUnit: string;
  quantity: number;
  pricePer100BaseUnits: number;
  totalPrice: number;
  isPurchased: boolean;
  category: number;
  note: string | null;
}
