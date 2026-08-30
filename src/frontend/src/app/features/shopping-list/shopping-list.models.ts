export interface ShoppingListItem {
  id: string;
  ingredientId?: string | null;
  preparedMealId?: string | null;
  customName?: string | null;
  ingredientName: string;
  measurementUnit: string;
  quantity: number;
  pricePer100BaseUnits: number;
  totalPrice: number;
  isPurchased: boolean;
  category: number;
  note: string | null;
}

export type ShoppingListUpdate = Pick<ShoppingListItem, 'quantity' | 'isPurchased' | 'note'>;
