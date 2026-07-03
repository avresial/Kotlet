import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../core/http/api-url';
import { ShoppingListItem } from './shopping-list.models';

@Injectable({ providedIn: 'root' })
export class ShoppingListService {
  private readonly http = inject(HttpClient);
  getAll() { return this.http.get<ShoppingListItem[]>(apiUrl('/api/shopping-list')); }
  create(ingredientId: string, quantity: number) { return this.http.post<ShoppingListItem>(apiUrl('/api/shopping-list'), { ingredientId, quantity }); }
  update(item: ShoppingListItem, changes: Partial<Pick<ShoppingListItem, 'quantity' | 'isPurchased'>>) {
    return this.http.put<ShoppingListItem>(apiUrl(`/api/shopping-list/${item.id}`), {
      quantity: changes.quantity ?? item.quantity,
      isPurchased: changes.isPurchased ?? item.isPurchased,
    });
  }
  delete(id: string) { return this.http.delete<void>(apiUrl(`/api/shopping-list/${id}`)); }
  clearChecked() { return this.http.delete<{ removed: number }>(apiUrl('/api/shopping-list/checked')); }
  generate(from: string, to: string) {
    return this.http.post<ShoppingListItem[]>(apiUrl('/api/shopping-list/generate'), { from, to });
  }
}
