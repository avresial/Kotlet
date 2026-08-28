import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { AuthService } from '../../../../core/auth/auth.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { MealPlanItem } from '../../../meal-planner/models/meal-planner.models';
import { MealPlannerService } from '../../../meal-planner/services/meal-planner.service';
import { ShoppingListIntegrationService } from '../../../meal-planner/services/shopping-list-integration.service';
import { RecipeDetail } from '../../models/recipe.models';
import { RecipeShoppingList } from './recipe-shopping-list';

const ingredient = {
  id: 'pasta',
  name: 'Pasta',
  caloriesPer100BaseUnits: 100,
  pricePer100BaseUnits: 5,
  isCountable: false,
  measurementUnit: 'g',
} as Ingredient;

const recipe: RecipeDetail = {
  id: 'pasta-recipe',
  title: 'Pasta dinner',
  slug: 'pasta-dinner',
  createdByUserId: 'user-1',
  descriptionMarkdown: null,
  servings: 4,
  mealType: null,
  ingredients: [{
    id: 'line', sortOrder: 0, ingredientId: ingredient.id, name: ingredient.name,
    quantity: 400, unit: 'g', normalizedQuantity: 400, normalizedUnit: 'g', note: null,
  }],
  images: [],
  canEdit: true,
  isAiAssisted: false,
  sourceUrl: null,
  videoUrl: null,
  videoThumbnailUrl: null,
  createdAtUtc: '2026-06-28T00:00:00Z',
  updatedAtUtc: '2026-06-28T00:00:00Z',
};

function setup(members: { userId: string; displayName: string }[] = []) {
  const integration = { addToShoppingList: vi.fn().mockReturnValue(of([])) };
  TestBed.configureTestingModule({
    imports: [RecipeShoppingList],
    providers: [
      { provide: MealPlannerService, useValue: { getHouseMembers: () => of(members) } },
      { provide: ShoppingListIntegrationService, useValue: integration },
      { provide: AuthService, useValue: { currentUser: () => ({ id: 'user-1', displayName: 'Alex' }) } },
      { provide: TranslationService, useValue: { translate: (key: string) => key } },
    ],
  });
  const fixture = TestBed.createComponent(RecipeShoppingList);
  fixture.componentRef.setInput('recipe', recipe);
  fixture.componentRef.setInput('ingredients', [ingredient]);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, integration };
}

describe('RecipeShoppingList', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('seeds the current user at a full portion when opened', () => {
    const { component } = setup();
    component.open();
    expect(component.participants()).toEqual([
      { userId: 'user-1', displayName: 'Alex', isCurrentUser: true, portionPercent: 100 },
    ]);
    expect(component.totalServings()).toBe(1);
  });

  it('sums participant portions and guests into the total servings', () => {
    const { component } = setup([{ userId: 'user-2', displayName: 'Sam' }]);
    component.open();
    component.addParticipant('user-2');
    component.addGuest();
    // 100% + 100% + 1 guest = 3 servings
    expect(component.totalServings()).toBe(3);
  });

  it('scales shopping quantities by the chosen servings', () => {
    const { component, integration } = setup();
    component.open();
    component.addGuest(); // 1 participant (100%) + 1 guest = 2 servings
    component.addToShoppingList();

    const item = integration.addToShoppingList.mock.calls[0][0] as MealPlanItem;
    expect(item.type).toBe('recipe');
    expect(item.recipeId).toBe('pasta-recipe');
    expect(item.servings).toBe(2);
    expect(component.state()).toBe('added');
  });

  it('refuses to add when nobody is eating', () => {
    const { component, integration } = setup();
    component.open();
    component.clearParticipants();
    component.addToShoppingList();
    expect(integration.addToShoppingList).not.toHaveBeenCalled();
    expect(component.error()).toBe('recipes.shoppingNoPeople');
  });

  it('clamps an out-of-range portion percentage on commit', () => {
    const { component } = setup();
    component.open();
    const person = component.participants()[0];
    const input = { value: '400' } as HTMLInputElement;
    component.commitField(person, 'portion', input);
    expect(input.value).toBe('150');
    expect(component.participants()[0].portionPercent).toBe(150);
  });
});
