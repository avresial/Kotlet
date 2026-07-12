import { Ingredient } from '../ingredients/ingredient.models';
import { RecipeDetail } from '../recipes/models/recipe.models';
import { MealPlanItem } from './models/meal-planner.models';

export interface PersonCalories {
  id: string;
  name: string;
  calories: number;
}

export function allocateCaloriesByPerson(
  items: MealPlanItem[],
  recipeDetails: Record<string, RecipeDetail>,
  ingredients: readonly Ingredient[],
  guestLabel: string,
  unassignedLabel: string,
): PersonCalories[] {
  function getCaloriesPerServing(item: MealPlanItem): number | null {
    if (item.type === 'ingredient') {
      const ingredient = ingredients.find((candidate) => candidate.id === item.ingredientId);
      return ingredient ? directIngredientCaloriesPerServing(ingredient) : null;
    }

    const detail = item.recipeId ? recipeDetails[item.recipeId] : undefined;
    return detail ? recipeCaloriesPerServing(detail, ingredients) : null;
  }

  const totals = new Map<string, PersonCalories>();
  let guestCalories = 0;
  let unassignedCalories = 0;

  for (const item of items) {
    const caloriesPerServing = getCaloriesPerServing(item);
    if (caloriesPerServing === null || item.servings === 0) continue;
    const headcount = item.participants.length + item.guests;
    if (headcount === 0) {
      unassignedCalories += caloriesPerServing * item.servings;
      continue;
    }

    const caloriesPerPerson = caloriesPerServing * item.servings / headcount;
    for (const participant of item.participants) {
      const existing = totals.get(participant.userId);
      totals.set(participant.userId, {
        id: participant.userId,
        name: participant.displayName,
        calories: (existing?.calories ?? 0) + caloriesPerPerson,
      });
    }
    guestCalories += caloriesPerPerson * item.guests;
  }

  const result = [...totals.values()].sort((a, b) => b.calories - a.calories || a.name.localeCompare(b.name));
  if (guestCalories > 0) result.push({ id: 'guests', name: guestLabel, calories: guestCalories });
  if (unassignedCalories > 0) result.push({ id: 'unassigned', name: unassignedLabel, calories: unassignedCalories });
  return result;
}

export function recipePricePerServing(recipe: RecipeDetail, ingredients: readonly Ingredient[]): number {
  const batchPrice = recipe.ingredients.reduce((total, recipeIngredient) => {
    const ingredient = ingredients.find((candidate) => candidate.id === recipeIngredient.ingredientId);
    return total + (ingredient
      ? ingredient.pricePer100BaseUnits * recipeIngredient.normalizedQuantity / 100
      : 0);
  }, 0);

  return batchPrice / recipe.servings;
}

export function recipeCaloriesPerServing(recipe: RecipeDetail, ingredients: readonly Ingredient[]): number {
  const batchCalories = recipe.ingredients.reduce((total, recipeIngredient) => {
    const ingredient = ingredients.find((candidate) => candidate.id === recipeIngredient.ingredientId);
    return total + (ingredient
      ? ingredient.caloriesPer100BaseUnits * recipeIngredient.normalizedQuantity / 100
      : 0);
  }, 0);

  return batchCalories / recipe.servings;
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

export function directIngredientCaloriesPerServing(ingredient: Ingredient): number {
  return ingredient.caloriesPer100BaseUnits * directIngredientQuantity(ingredient, 1) / 100;
}
