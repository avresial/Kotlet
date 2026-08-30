import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { PreparedMeal } from '../../../prepared-meals/prepared-meal.models';
import { ShoppingAdd, ShoppingAddRequest } from './shopping-add';

const ingredient = (id: string, name: string, extra: Partial<Ingredient> = {}): Ingredient => ({
  id, name, defaultName: name, translation: null, measurementUnit: 'g', isCountable: false,
  measurementUnitsPerPiece: null, caloriesPer100BaseUnits: 0, pricePer100BaseUnits: 5, svgIcon: null,
  category: 0, allergens: 0, attributes: 0, suitability: 0, isAiModified: false,
  createdAtUtc: '2026-01-01T00:00:00Z', ...extra,
});
const meal = (id: string, name: string, brand: string | null = null): PreparedMeal =>
  ({ id, name, brand, servings: 2, caloriesPerServing: 300, isArchived: false, addons: [] }) as PreparedMeal;

describe('ShoppingAdd', () => {
  let fixture: ComponentFixture<ShoppingAdd>;
  let component: ShoppingAdd;
  let added: ShoppingAddRequest[];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ShoppingAdd],
      providers: [{ provide: TranslationService, useValue: { translate: (key: string) => key } }],
    });
    fixture = TestBed.createComponent(ShoppingAdd);
    fixture.componentRef.setInput('ingredients', [
      ingredient('pasta', 'Pasta'), ingredient('parmesan', 'Parmesan'), ingredient('milk', 'Milk', { measurementUnit: 'ml' }),
    ]);
    fixture.componentRef.setInput('preparedMeals', [meal('gyoza', 'Gyoza', 'Ajinomoto')]);
    component = fixture.componentInstance;
    added = [];
    component.add.subscribe(request => added.push(request));
    fixture.detectChanges();
  });

  it('searches ingredients and ready meals from the one input', () => {
    component.onInput('a');

    expect(component.suggestions().map(option => [option.kind, option.name])).toEqual([
      ['preparedMeal', 'Gyoza — Ajinomoto'], ['ingredient', 'Parmesan'], ['ingredient', 'Pasta'],
    ]);
  });

  it('puts prefix matches first', () => {
    component.onInput('pa');

    expect(component.suggestions().map(option => option.name)).toEqual(['Parmesan', 'Pasta']);
  });

  it('offers nothing until something is typed', () => {
    expect(component.suggestions()).toEqual([]);
    expect(component.listedMatches()).toEqual([]);
  });

  it('answers a search for something already on the list with how much of it is down', () => {
    fixture.componentRef.setInput('onList', [{ id: 'eggs', name: 'Eggs', quantity: 6, unit: 'piece' }]);

    component.onInput('egg');

    expect(component.suggestions()).toEqual([]);
    expect(component.listedMatches()).toEqual([{ id: 'eggs', name: 'Eggs', quantity: 6, unit: 'piece' }]);
  });

  it('reveals the quantity controls only once a product is picked', () => {
    expect(component.units()).toEqual([]);
    expect(component.canSubmit()).toBe(false);

    component.onInput('pas');
    component.select(component.suggestions()[0]);

    expect(component.units()).toEqual(['g', 'kg']);
    expect(component.unit()).toBe('g');
    expect(component.canSubmit()).toBe(true);
  });

  it('offers packages for a ready meal', () => {
    component.onInput('gyo');
    component.select(component.suggestions()[0]);

    expect(component.units()).toEqual(['package']);
  });

  it('steps the quantity in shop-sized amounts and never below one step', () => {
    component.onInput('pas');
    component.select(component.suggestions()[0]);

    component.step(1);
    expect(component.quantity()).toBe(51);

    component.setQuantity(120);
    component.step(-1);
    expect(component.quantity()).toBe(70);

    component.step(-1);
    expect(component.quantity()).toBe(50);
  });

  it('leaves a quantity already below one step alone rather than raising it', () => {
    component.onInput('pas');
    component.select(component.suggestions()[0]);
    component.setQuantity(20);

    component.step(-1);

    expect(component.quantity()).toBe(20);
  });

  it('walks the suggestions with the arrow keys and picks the active one with Enter', () => {
    const key = (name: string) => component.onSearchKeydown(new KeyboardEvent('keydown', { key: name }));
    component.onInput('pa');

    key('ArrowDown');
    expect(component.activeIndex()).toBe(1);
    key('ArrowDown');
    expect(component.activeIndex()).toBe(2);
    key('ArrowUp');
    key('ArrowUp');
    expect(component.activeIndex()).toBe(0);
    key('Enter');

    expect(component.selected()!.name).toBe('Parmesan');
  });

  it('drops the query on Escape without picking anything', () => {
    component.onInput('pa');

    component.onSearchKeydown(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(component.query()).toBe('');
    expect(component.selected()).toBeNull();
  });

  it('converts the quantity to the base unit when emitting', () => {
    component.onInput('pas');
    component.select(component.suggestions()[0]);
    component.setUnit('kg');
    component.setQuantity(1.5);
    component.note.set('  the long ones  ');
    component.submit();

    expect(added).toEqual([expect.objectContaining({
      baseQuantity: 1500, displayQuantity: 1.5, displayUnit: 'kg', note: 'the long ones',
    })]);
  });

  it('clears itself right after adding so the next product can be typed', () => {
    component.onInput('pas');
    component.select(component.suggestions()[0]);
    component.note.set('a note');
    component.submit();

    expect(component.selected()).toBeNull();
    expect(component.query()).toBe('');
    expect(component.note()).toBe('');
    expect(component.showNote()).toBe(false);
  });

  it('refuses to add a quantity of zero', () => {
    component.onInput('pas');
    component.select(component.suggestions()[0]);
    component.setQuantity(0);

    expect(component.canSubmit()).toBe(false);
    component.submit();
    expect(added).toEqual([]);
  });

  it('goes back to the search box when the picked product is dropped', () => {
    component.onInput('pas');
    component.select(component.suggestions()[0]);
    component.clearSelection();

    expect(component.selected()).toBeNull();
  });

  it('offers custom option when query is typed and gives package-style unit controls', () => {
    component.onInput('Paper towels');

    expect(component.customOption()).toEqual({
      kind: 'custom',
      name: 'Paper towels',
      hint: 'shopping.customItem',
    });

    component.select(component.customOption()!);

    expect(component.selected()).toEqual({
      kind: 'custom',
      name: 'Paper towels',
      hint: 'shopping.customItem',
    });
    expect(component.units()).toEqual(['package']);
    expect(component.unit()).toBe('package');
    expect(component.quantity()).toBe(1);
    expect(component.canSubmit()).toBe(true);
  });

  it('steps custom item quantity by package', () => {
    component.onInput('Paper towels');
    component.select(component.customOption()!);

    component.step(1);
    expect(component.quantity()).toBe(2);

    component.step(-1);
    expect(component.quantity()).toBe(1);
  });

  it('submits a custom item request with package unit and trimmed note', () => {
    component.onInput('  Paper towels  ');
    component.select(component.customOption()!);
    component.setQuantity(3);
    component.note.set('  recycled  ');
    component.submit();

    expect(added).toEqual([
      expect.objectContaining({
        option: { kind: 'custom', name: 'Paper towels', hint: 'shopping.customItem' },
        baseQuantity: 3,
        displayQuantity: 3,
        displayUnit: 'package',
        note: 'recycled',
      }),
    ]);
  });

  it('picks custom item on Enter when no catalogue suggestions match', () => {
    component.onInput('Toothpaste');
    component.onSearchKeydown(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(component.selected()).toEqual({
      kind: 'custom',
      name: 'Toothpaste',
      hint: 'shopping.customItem',
    });
  });

  it('includes the custom action in keyboard navigation after catalogue suggestions', () => {
    component.onInput('pa');

    expect(component.navigableOptions().map(option => option.name)).toEqual(['Parmesan', 'Pasta', 'pa']);

    component.onSearchKeydown(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    component.onSearchKeydown(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    component.onSearchKeydown(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(component.selected()).toEqual({ kind: 'custom', name: 'pa', hint: 'shopping.customItem' });
  });

  it('renders the custom action option in the dropdown list when search is non-empty', () => {
    component.onInput('Toothpaste');
    fixture.detectChanges();

    const customAction = fixture.nativeElement.querySelector('.custom-action');
    expect(customAction).not.toBeNull();
    expect(customAction.textContent).toContain('Toothpaste');
  });
});
