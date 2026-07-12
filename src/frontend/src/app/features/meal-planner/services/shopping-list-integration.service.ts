import { inject, Injectable } from '@angular/core';
import { forkJoin, Observable, switchMap } from 'rxjs';
import { Ingredient } from '../../ingredients/ingredient.models';
import { RecipeDetail } from '../../recipes/models/recipe.models';
import { ShoppingListService } from '../../shopping-list/shopping-list.service';
import { ShoppingListItem } from '../../shopping-list/shopping-list.models';
import {
  directIngredientQuantity,
  scaleRecipeQuantity,
} from '../meal-planner-calculations';
import { MealPlanItem } from '../models/meal-planner.models';

@Injectable({ providedIn: 'root' })
export class ShoppingListIntegrationService {
  private readonly shoppingListService = inject(ShoppingListService);

  addToShoppingList(
    item: MealPlanItem,
    recipeDetails: Record<string, RecipeDetail>,
    ingredients: readonly Ingredient[],
    noCatalogueIngredientMessage: string
  ): Observable<ShoppingListItem[]> {
    const quantities = this.calculateShoppingQuantities(item, recipeDetails, ingredients);
    if (!quantities.length) throw new Error(noCatalogueIngredientMessage);

    return this.mergeWithCurrentShoppingList(quantities);
  }

  private calculateShoppingQuantities(
    item: MealPlanItem,
    recipeDetails: Record<string, RecipeDetail>,
    ingredients: readonly Ingredient[]
  ): { ingredient: Ingredient; quantity: number }[] {
    if (item.type === 'ingredient') {
      const ingredient = ingredients.find((candidate) => candidate.id === item.ingredientId);
      if (!ingredient) return [];
      const quantity = directIngredientQuantity(ingredient, item.servings);
      return quantity > 0 ? [{ ingredient, quantity }] : [];
    }

    const detail = item.recipeId ? recipeDetails[item.recipeId] : undefined;
    const totals = new Map<string, { ingredient: Ingredient; quantity: number }>();
    for (const recipeIngredient of detail?.ingredients ?? []) {
      const ingredient = ingredients.find((candidate) => candidate.id === recipeIngredient.ingredientId);
      if (!ingredient) continue;
      const existing = totals.get(ingredient.id);
      const quantity = scaleRecipeQuantity(
        recipeIngredient.normalizedQuantity,
        detail!.servings,
        item.servings,
      );
      totals.set(ingredient.id, { ingredient, quantity: (existing?.quantity ?? 0) + quantity });
    }
    return [...totals.values()];
  }

  private mergeWithCurrentShoppingList(quantities: { ingredient: Ingredient; quantity: number }[]): Observable<ShoppingListItem[]> {
    return this.shoppingListService.getAll().pipe(
      switchMap((current) => forkJoin(quantities.map(({ ingredient, quantity }) => {
        const existing = current.find((entry) => entry.ingredientId === ingredient.id);
        return existing
          ? this.shoppingListService.update(existing, { quantity: existing.quantity + quantity, isPurchased: false })
          : this.shoppingListService.create(ingredient.id, quantity);
      })))
    );
  }
}
