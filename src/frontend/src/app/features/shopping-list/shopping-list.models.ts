export interface ShoppingListItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  measurementUnit: string;
  quantity: number;
  pricePer100BaseUnits: number;
  totalPrice: number;
  isPurchased: boolean;
}
