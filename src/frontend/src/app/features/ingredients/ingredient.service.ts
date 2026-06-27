import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Ingredient, IngredientRequest } from './ingredient.models';

@Injectable({ providedIn: 'root' })
export class IngredientService {
  private readonly http = inject(HttpClient);

  getAll() { return this.http.get<Ingredient[]>('/api/ingredients'); }
  create(request: IngredientRequest) { return this.http.post<Ingredient>('/api/ingredients', request); }
  update(id: string, request: IngredientRequest) { return this.http.put<Ingredient>(`/api/ingredients/${id}`, request); }
  delete(id: string) { return this.http.delete<void>(`/api/ingredients/${id}`); }
}
