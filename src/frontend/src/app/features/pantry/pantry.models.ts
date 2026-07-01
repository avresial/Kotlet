export interface PantryItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  measurementUnit: string;
  quantity: number;
  expirationDate: string | null;
  storageLocation: 1 | 2 | 3 | null;
}

export const storageLocations = [
  { value: 1, label: 'pantry.location.refrigerator' },
  { value: 2, label: 'pantry.location.freezer' },
  { value: 3, label: 'pantry.location.cabinet' },
] as const;
