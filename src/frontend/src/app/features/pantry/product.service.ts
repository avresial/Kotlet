import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../core/http/api-url';
import {
  Product,
  ProductLookupResult,
  ProductRequest,
  ProductStock,
  ProductStockCreateRequest,
  ProductStockUpdateRequest,
} from './product.models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly http = inject(HttpClient);

  getProducts() {
    return this.http.get<Product[]>(apiUrl('/api/products'));
  }

  lookup(barcode: string, refreshNutrition = false) {
    return this.http.get<ProductLookupResult>(
      apiUrl(
        `/api/products/lookup/${encodeURIComponent(barcode)}${refreshNutrition ? '?refreshNutrition=true' : ''}`,
      ),
    );
  }

  saveProduct(barcode: string, request: ProductRequest) {
    return this.http.put<Product>(apiUrl(`/api/products/${encodeURIComponent(barcode)}`), request);
  }

  getStock() {
    return this.http.get<ProductStock[]>(apiUrl('/api/product-stock'));
  }

  addStock(request: ProductStockCreateRequest) {
    return this.http.post<ProductStock>(apiUrl('/api/product-stock'), request);
  }

  updateStock(id: string, request: ProductStockUpdateRequest) {
    return this.http.put<ProductStock>(apiUrl(`/api/product-stock/${id}`), request);
  }

  deleteStock(id: string) {
    return this.http.delete<void>(apiUrl(`/api/product-stock/${id}`));
  }
}
