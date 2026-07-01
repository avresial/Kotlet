import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../core/http/api-url';

export interface FoodSettings { avoidedAllergens: number; avoidedAttributes: number; requiredSuitability: number; excludedIngredientIds: string[]; }

@Injectable({ providedIn: 'root' })
export class FoodSettingsService {
  private readonly http = inject(HttpClient);
  get() { return this.http.get<FoodSettings>(apiUrl('/api/users/me/food-settings')); }
  save(value: FoodSettings) { return this.http.put<FoodSettings>(apiUrl('/api/users/me/food-settings'), value); }
}
