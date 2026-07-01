export interface PantryItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  measurementUnit: string;
  quantity: number;
  expirationDate: string | null;
}
