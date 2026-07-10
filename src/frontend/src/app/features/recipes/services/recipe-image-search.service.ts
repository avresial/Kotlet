import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../../core/http/api-url';
import { RecipeImageCandidate } from '../models/recipe.models';

const genericIngredients = new Set(['salt', 'water', 'oil', 'pepper', 'black pepper', 'olive oil']);

export function buildRecipeImageSearchQuery(title: string, ingredients: readonly string[] = []): string {
  const meaningfulIngredients = ingredients
    .map(ingredient => ingredient.trim())
    .filter(ingredient => ingredient && !genericIngredients.has(ingredient.toLowerCase()))
    .filter((ingredient, index, values) => values.findIndex(value => value.toLowerCase() === ingredient.toLowerCase()) === index)
    .slice(0, 3);
  return [title.trim(), ...meaningfulIngredients].filter(Boolean).join(' ');
}

@Injectable({ providedIn: 'root' })
export class RecipeImageSearchService {
  private readonly http = inject(HttpClient);

  search(query: string, limit = 10) {
    const params = new HttpParams()
      .set('query', query)
      .set('limit', limit)
      .set('orientation', 'landscape');
    return this.http.get<RecipeImageCandidate[]>(apiUrl('/api/recipes/images/search'), { params });
  }
}
