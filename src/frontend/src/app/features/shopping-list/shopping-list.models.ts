export interface ShoppingListItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  measurementUnit: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  isPurchased: boolean;
}
