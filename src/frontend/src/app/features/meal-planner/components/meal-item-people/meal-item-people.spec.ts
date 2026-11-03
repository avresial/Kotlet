import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { HouseMember, MealParticipant, MealPlanItem } from '../../models/meal-planner.models';
import { MealItemPeople, ParticipantPortionChange } from './meal-item-people';

const ingredient: Ingredient = {
  id: 'eggs',
  name: 'Eggs',
  defaultName: 'Eggs',
  translation: null,
  measurementUnit: 'g',
  isCountable: true,
  measurementUnitsPerPiece: 50,
  caloriesPer100BaseUnits: 155,
  pricePer100BaseUnits: 50,
  svgIcon: null,
  category: 0,
  allergens: 0,
  attributes: 0,
  suitability: 0,
  isAiModified: false,
  createdAtUtc: '2026-01-01T00:00:00Z',
};

// One piece (50 g) of eggs → 155 * 50 / 100 = 77.5 kcal per standard serving.
const caloriesPerServing = 77.5;

const participant: MealParticipant = { userId: 'asik', displayName: 'Asik', isCurrentUser: false, portionPercent: 100 };

const item: MealPlanItem = {
  id: 'item-1',
  slot: 'breakfast',
  type: 'ingredient',
  ingredientId: ingredient.id,
  displayName: 'Eggs',
  sortOrder: 0,
  participants: [participant],
  guests: 0,
  servings: 1,
  servingsOverridden: false,
};

function fakeInput(value: string): HTMLInputElement {
  const input = document.createElement('input');
  input.value = value;
  return input;
}

interface Emitted {
  portions: ParticipantPortionChange[];
  participants: string[][];
  guests: number[];
}

function createComponent(members: HouseMember[] = [], mealItem: MealPlanItem = item): { component: MealItemPeople; emitted: Emitted } {
  TestBed.configureTestingModule({
    providers: [{ provide: TranslationService, useValue: { translate: (key: string) => key, language: () => 'en' } }],
  });
  const fixture = TestBed.createComponent(MealItemPeople);
  fixture.componentRef.setInput('item', mealItem);
  fixture.componentRef.setInput('members', members);
  fixture.componentRef.setInput('ingredients', [ingredient]);
  fixture.componentRef.setInput('caloriesPerServing', caloriesPerServing);
  fixture.detectChanges();

  const component = fixture.componentInstance;
  const emitted: Emitted = { portions: [], participants: [], guests: [] };
  component.portionChange.subscribe((change) => emitted.portions.push(change));
  component.participantsChange.subscribe((ids) => emitted.participants.push(ids));
  component.guestsChange.subscribe((guests) => emitted.guests.push(guests));
  return { component, emitted };
}

describe('MealItemPeople portion validation', () => {
  it('flags an out-of-range serving percentage as the user types', () => {
    const { component } = createComponent();

    component.validateParticipantField(participant, 'portion', '500');
    expect(component.fieldError(participant, 'portion')).toBe('meal.rangeError');
    expect(component.itemFieldErrors()).toEqual(['meal.rangeError']);

    component.validateParticipantField(participant, 'portion', '120');
    expect(component.fieldError(participant, 'portion')).toBeNull();
    expect(component.itemFieldErrors()).toEqual([]);
  });

  it('does not flag an empty field while the user is mid-edit', () => {
    const { component } = createComponent();

    component.validateParticipantField(participant, 'portion', '');
    expect(component.fieldError(participant, 'portion')).toBeNull();
  });

  it('clamps an out-of-range serving percentage on commit and resets the field', () => {
    const { component, emitted } = createComponent();

    const input = fakeInput('500');
    component.commitParticipantField(participant, 'portion', input);

    expect(emitted.portions).toEqual([{ userId: participant.userId, portionPercent: 150 }]);
    expect(input.value).toBe('150');
    expect(component.fieldError(participant, 'portion')).toBeNull();
  });

  it('clamps out-of-range calories back into the allowed range on commit', () => {
    const { component, emitted } = createComponent();

    // 500 kcal is far above the 1.5-serving ceiling (~116 kcal), so it clamps to 150%.
    const input = fakeInput('500');
    component.commitParticipantField(participant, 'calories', input);

    expect(emitted.portions).toEqual([{ userId: participant.userId, portionPercent: 150 }]);
    expect(input.value).toBe((caloriesPerServing * 1.5).toFixed(0));
  });

  it('reverts an empty field to the current portion without saving', () => {
    const { component, emitted } = createComponent();

    const input = fakeInput('');
    component.commitParticipantField(participant, 'portion', input);

    expect(emitted.portions).toEqual([]);
    expect(input.value).toBe('100');
  });

  it('does not emit when the committed portion is unchanged', () => {
    const { component, emitted } = createComponent();

    component.commitParticipantField(participant, 'portion', fakeInput('100'));

    expect(emitted.portions).toEqual([]);
  });
});

describe('MealItemPeople people and guests', () => {
  const members: HouseMember[] = [
    { userId: 'asik', displayName: 'Asik' },
    { userId: 'bob', displayName: 'Bob' },
  ];

  it('offers only unassigned members and adds the whole house', () => {
    const { component, emitted } = createComponent(members);

    expect(component.availableMembers().map((member) => member.userId)).toEqual(['bob']);
    component.addHouse();

    expect(emitted.participants).toEqual([['asik', 'bob']]);
  });

  it('keeps guests within bounds', () => {
    const { component, emitted } = createComponent(members, { ...item, guests: 0 });

    component.removeGuest();
    component.addGuest();

    expect(emitted.guests).toEqual([1]);
  });
});
