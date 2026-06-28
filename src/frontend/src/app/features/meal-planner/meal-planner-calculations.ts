import { Ingredient } from '../ingredients/ingredient.models';
import { RecipeDetail } from '../recipes/models/recipe.models';

export function recipePricePerServing(recipe: RecipeDetail, ingredients: readonly Ingredient[]): number {
  const batchPrice = recipe.ingredients.reduce((total, recipeIngredient) => {
    const ingredient = ingredients.find((candidate) => candidate.id === recipeIngredient.ingredientId);
    return total + (ingredient
      ? ingredient.pricePer100BaseUnits * recipeIngredient.normalizedQuantity / 100
      : 0);
  }, 0);

  return batchPrice / recipe.servings;
}

export function scaleRecipeQuantity(quantity: number, recipeServings: number, peopleServed: number): number {
  return quantity / recipeServings * peopleServed;
}

export function directIngredientQuantity(ingredient: Ingredient, servings: number): number {
  const quantityPerServing = ingredient.isCountable
    ? ingredient.measurementUnitsPerPiece ?? 0
    : 1;
  return quantityPerServing * Math.max(servings, 1);
}
