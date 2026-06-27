import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Ingredient, IngredientRequest } from './ingredient.models';
import { apiUrl } from '../../core/http/api-url';

@Injectable({ providedIn: 'root' })
export class IngredientService {
  private readonly http = inject(HttpClient);

  getAll() { return this.http.get<Ingredient[]>(apiUrl('/api/ingredients')); }
  create(request: IngredientRequest) { return this.http.post<Ingredient>(apiUrl('/api/ingredients'), request); }
  update(id: string, request: IngredientRequest) { return this.http.put<Ingredient>(apiUrl(`/api/ingredients/${id}`), request); }
  delete(id: string) { return this.http.delete<void>(apiUrl(`/api/ingredients/${id}`)); }
}
