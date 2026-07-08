import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../core/http/api-url';
import { PantryItem, PantryRecipeMatch } from './pantry.models';

@Injectable({ providedIn: 'root' })
export class PantryService {
  private readonly http = inject(HttpClient);
  getAll() { return this.http.get<PantryItem[]>(apiUrl('/api/pantry')); }
  getRecipeMatches() { return this.http.get<PantryRecipeMatch[]>(apiUrl('/api/pantry/recipe-matches')); }
  create(ingredientId: string, quantity: number, expirationDate: string | null, storageLocation: number) { return this.http.post<PantryItem>(apiUrl('/api/pantry'), { ingredientId, quantity, expirationDate, storageLocation }); }
  update(id: string, quantity: number) { return this.http.put<PantryItem>(apiUrl(`/api/pantry/${id}`), { quantity }); }
  delete(id: string) { return this.http.delete<void>(apiUrl(`/api/pantry/${id}`)); }
}
