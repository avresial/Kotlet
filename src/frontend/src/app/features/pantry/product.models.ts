export type ProductUnit = 'g' | 'ml' | 'pcs';
export type NutritionBasis = 'g' | 'ml';
export type ProductSource = 'Manual' | 'OpenFoodFacts' | 'FoodDataCentral';
export type StorageLocationValue = 1 | 2 | 3;

export interface Product {
  id: string;
  /** Canonical GTIN-14. */
  barcode: string;
  name: string;
  brand: string | null;
  packageDescription: string | null;
  packageQuantity: number;
  unit: ProductUnit;
  ingredientId: string | null;
  caloriesPer100: number | null;
  proteinPer100: number | null;
  carbsPer100: number | null;
  fatPer100: number | null;
  nutritionBasis: NutritionBasis | null;
  source: ProductSource;
  sourceUrl: string | null;
  sourceRetrievedAtUtc: string | null;
  foodDataCentralId?: number | null;
  foodDataCentralRetrievedAtUtc?: string | null;
}

export interface ProductRequest {
  name: string;
  brand: string | null;
  packageDescription: string | null;
  packageQuantity: number;
  unit: ProductUnit;
  ingredientId: string | null;
  caloriesPer100: number | null;
  proteinPer100: number | null;
  carbsPer100: number | null;
  fatPer100: number | null;
  nutritionBasis: NutritionBasis | null;
  source: ProductSource;
  foodDataCentralId?: number | null;
}

export type ProductLookupStatus =
  'local' | 'open_food_facts' | 'food_data_central' | 'not_found' | 'unavailable' | 'rate_limited';

export interface ProductLookupResult {
  status: ProductLookupStatus;
  product: Product | null;
}

export interface ProductStock {
  id: string;
  product: Product;
  /** Quantity in the product's base unit (g, ml or pcs). */
  quantity: number;
  /** yyyy-MM-dd */
  expirationDate: string | null;
  storageLocation: StorageLocationValue | null;
}

export interface ProductStockCreateRequest {
  productId: string;
  quantity: number;
  expirationDate: string | null;
  storageLocation: StorageLocationValue | null;
}

export interface ProductStockUpdateRequest {
  quantity: number;
  expirationDate: string | null;
  storageLocation: StorageLocationValue | null;
}

/** Id the backend gives to an Open Food Facts draft that is not yet part of the catalog. */
export const unsavedProductId = '00000000-0000-0000-0000-000000000000';

export const productUnits: readonly ProductUnit[] = ['g', 'ml', 'pcs'];
export const nutritionBases: readonly NutritionBasis[] = ['g', 'ml'];

export const openFoodFactsProductBaseUrl = 'https://world.openfoodfacts.org/product/';
export const openFoodFactsHost = 'openfoodfacts.org';
export const odblLicenseUrl = 'https://opendatacommons.org/licenses/odbl/1-0/';
