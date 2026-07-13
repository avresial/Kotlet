import { Ingredient } from '../ingredients/ingredient.models';
import { RecipeDetail } from '../recipes/models/recipe.models';
import { MealPlanItem, MealSlot } from './models/meal-planner.models';

export interface PersonCalories {
  id: string;
  name: string;
  meals: Record<MealSlot, number>;
  total: number;
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

  const emptyMeals = (): Record<MealSlot, number> => ({
    breakfast: 0, 'second-breakfast': 0, dinner: 0, snack: 0, supper: 0,
  });
  const totals = new Map<string, PersonCalories>();

  function add(id: string, name: string, slot: MealSlot, calories: number): void {
    const person = totals.get(id) ?? { id, name, meals: emptyMeals(), total: 0 };
    person.meals[slot] += calories;
    person.total += calories;
    totals.set(id, person);
  }

  for (const item of items) {
    const caloriesPerServing = getCaloriesPerServing(item);
    if (caloriesPerServing === null || item.servings === 0) continue;
    if (item.participants.length + item.guests === 0) {
      add('unassigned', unassignedLabel, item.slot, caloriesPerServing * item.servings);
      continue;
    }

    for (const participant of item.participants) {
      add(participant.userId, participant.displayName, item.slot,
        caloriesPerServing * participant.portionPercent / 100);
    }
    if (item.guests > 0) add('guests', guestLabel, item.slot, caloriesPerServing * item.guests);
  }

  return [...totals.values()].sort((a, b) => b.total - a.total || a.name.localeCompare(b.name));
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
    : 100;
  return quantityPerServing * Math.max(servings, 0);
}

export function directIngredientCaloriesPerServing(ingredient: Ingredient): number {
  return ingredient.caloriesPer100BaseUnits * directIngredientQuantity(ingredient, 1) / 100;
}
