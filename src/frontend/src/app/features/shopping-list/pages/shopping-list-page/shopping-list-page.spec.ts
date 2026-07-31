import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { ShoppingListItem } from '../../shopping-list.models';
import { ShoppingListService } from '../../shopping-list.service';
import { PreparedMealService } from '../../../prepared-meals/prepared-meal.service';
import { PreparedMeal } from '../../../prepared-meals/prepared-meal.models';
import { ShoppingAddRequest } from '../../components/shopping-add/shopping-add';
import { groupShoppingItems, ShoppingListPage } from './shopping-list-page';

const item = (id: string, category: number, note: string | null = null): ShoppingListItem => ({
  id, ingredientId: id, ingredientName: id, measurementUnit: 'g', quantity: 1,
  pricePer100BaseUnits: 1, totalPrice: 1, isPurchased: false, category, note,
});
const bought = (id: string, category: number): ShoppingListItem => ({ ...item(id, category), isPurchased: true });
const summarize = (groups: ReturnType<typeof groupShoppingItems>) =>
  groups.map(group => [group.key, group.items.map(value => value.id)]);

describe('groupShoppingItems', () => {
  it('groups categorized and unknown products in category order', () => {
    const groups = groupShoppingItems([item('apple', 21), item('other', 0), item('pear', 21)]);

    expect(summarize(groups)).toEqual([['category-0', ['other']], ['category-21', ['apple', 'pear']]]);
  });

  it('collects bought items into a single group after the remaining categories', () => {
    const groups = groupShoppingItems([bought('apple', 21), item('other', 0), bought('egg', 5), item('pear', 21)]);

    expect(summarize(groups)).toEqual([
      ['category-0', ['other']], ['category-21', ['pear']], ['bought', ['egg', 'apple']],
    ]);
    expect(groups.map(group => group.isBought)).toEqual([false, false, true]);
    expect(groups.at(-1)!.label).toBe('shopping.alreadyBought');
  });

  it('omits the bought group while nothing is ticked off', () => {
    expect(groupShoppingItems([item('pear', 21)]).some(group => group.isBought)).toBe(false);
  });
});

const ingredient: Ingredient = {
  id: 'pasta', name: 'Pasta', defaultName: 'Pasta', translation: null, measurementUnit: 'g',
  isCountable: false, measurementUnitsPerPiece: null, caloriesPer100BaseUnits: 0,
  pricePer100BaseUnits: 5, svgIcon: null, category: 0, allergens: 0, attributes: 0,
  suitability: 0, isAiModified: false, createdAtUtc: '2026-01-01T00:00:00Z',
};

const addIngredient = (quantity: number, note: string): ShoppingAddRequest => ({
  option: { kind: 'ingredient', id: ingredient.id, name: ingredient.name, hint: 'g', ingredient },
  baseQuantity: quantity, displayQuantity: quantity, displayUnit: 'g', note,
});

describe('ShoppingListPage notes', () => {
  let page: ShoppingListPage;
  let shoppingListService: { getAll: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn>; update: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    shoppingListService = {
      getAll: vi.fn().mockReturnValue(of([])),
      create: vi.fn().mockImplementation((id: string) => of(item(id, 0))),
      update: vi.fn().mockImplementation((existing: ShoppingListItem, changes: Partial<ShoppingListItem>) => of({ ...existing, ...changes })),
    };
    TestBed.configureTestingModule({
      providers: [
        ShoppingListPage,
        { provide: ShoppingListService, useValue: shoppingListService },
        { provide: IngredientService, useValue: { getAll: vi.fn().mockReturnValue(of([ingredient])) } },
        { provide: PreparedMealService, useValue: { list: vi.fn().mockReturnValue(of([])) } },
        { provide: TranslationService, useValue: { translate: (key: string) => key } },
      ],
    });
    page = TestBed.inject(ShoppingListPage);
    page.ngOnInit();
  });

  it('creates the item with the note the add panel handed over', () => {
    page.add(addIngredient(1, 'buy the fresh one'));
    expect(shoppingListService.create).toHaveBeenCalledWith(ingredient.id, 1, 'buy the fresh one');
  });

  it('creates with a null note when no note was entered', () => {
    page.add(addIngredient(1, ''));
    expect(shoppingListService.create).toHaveBeenCalledWith(ingredient.id, 1, null);
  });

  it('updates the note with the trimmed value', () => {
    page.updateNote(item('pasta', 0, null), '  organic  ');
    expect(shoppingListService.update).toHaveBeenCalledWith(expect.objectContaining({ id: 'pasta' }), { note: 'organic' });
  });

  it('clears an existing note with an empty string', () => {
    page.updateNote(item('pasta', 0, 'old note'), '   ');
    expect(shoppingListService.update).toHaveBeenCalledWith(expect.objectContaining({ id: 'pasta' }), { note: '' });
  });

  it('skips the update when the note is unchanged', () => {
    page.updateNote(item('pasta', 0, 'same'), 'same');
    page.updateNote(item('pasta', 0, null), '   ');
    expect(shoppingListService.update).not.toHaveBeenCalled();
  });
});

describe('ShoppingListPage re-adding a bought item', () => {
  let shoppingListService: { getAll: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn>; update: ReturnType<typeof vi.fn> };
  let storedItems: ShoppingListItem[];
  const boughtPasta: ShoppingListItem = {
    ...item('pasta', 0, 'from the last shop'), isPurchased: true, quantity: 75, totalPrice: 3.75,
  };

  /** getAll() reads storedItems lazily, so each test picks the list it starts from. */
  const loadPage = (items: ShoppingListItem[]): ShoppingListPage => {
    storedItems = items;
    const page = TestBed.inject(ShoppingListPage);
    page.ngOnInit();
    return page;
  };

  beforeEach(() => {
    storedItems = [];
    shoppingListService = {
      getAll: vi.fn().mockImplementation(() => of(storedItems)),
      create: vi.fn().mockImplementation((id: string) => of(item(id, 0))),
      update: vi.fn().mockImplementation((existing: ShoppingListItem, changes: Partial<ShoppingListItem>) => of({ ...existing, ...changes })),
    };
    TestBed.configureTestingModule({
      providers: [
        ShoppingListPage,
        { provide: ShoppingListService, useValue: shoppingListService },
        { provide: IngredientService, useValue: { getAll: vi.fn().mockReturnValue(of([ingredient])) } },
        { provide: PreparedMealService, useValue: { list: vi.fn().mockReturnValue(of([])) } },
        { provide: TranslationService, useValue: { translate: (key: string) => key } },
      ],
    });
  });

  it('offers an ingredient again once it has been ticked off', () => {
    const page = loadPage([boughtPasta]);

    expect(page.availableIngredients().map(value => value.id)).toEqual(['pasta']);
  });

  it('keeps an ingredient hidden while it is still waiting to be bought', () => {
    const page = loadPage([item('pasta', 0)]);

    expect(page.availableIngredients()).toEqual([]);
  });

  it('resets the bought line instead of creating a duplicate', () => {
    const page = loadPage([boughtPasta]);

    page.add(addIngredient(200, ''));

    expect(shoppingListService.create).not.toHaveBeenCalled();
    expect(shoppingListService.update).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'pasta' }),
      { quantity: 200, isPurchased: false, note: '' },
    );
    expect(page.items()).toHaveLength(1);
    expect(page.items()[0]).toMatchObject({ id: 'pasta', quantity: 200, isPurchased: false, note: '' });
  });

  it('still creates a new item when nothing matching is on the list', () => {
    const page = loadPage([]);

    page.add(addIngredient(1, ''));

    expect(shoppingListService.update).not.toHaveBeenCalled();
    expect(shoppingListService.create).toHaveBeenCalledWith('pasta', 1, null);
    expect(page.items()).toHaveLength(1);
  });

  it('puts ready meals in their own group', () => {
    const readyMeal = { ...item('gyoza', 0), ingredientId: null, preparedMealId: 'gyoza' };

    expect(groupShoppingItems([readyMeal]).map(group => [group.key, group.items[0].id]))
      .toEqual([['ready-meals', 'gyoza']]);
  });
});

describe('ShoppingListPage background adds', () => {
  let page: ShoppingListPage;
  let created: Subject<ShoppingListItem>;
  let shoppingListService: {
    getAll: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn>;
    createPreparedMeal: ReturnType<typeof vi.fn>; update: ReturnType<typeof vi.fn>;
    generate: ReturnType<typeof vi.fn>; clearChecked: ReturnType<typeof vi.fn>;
  };
  const meal = { id: 'gyoza', name: 'Gyoza', servings: 2, caloriesPerServing: 300, isArchived: false, addons: [] } as PreparedMeal;
  const addMeal = (quantity: number, note = ''): ShoppingAddRequest => ({
    option: { kind: 'preparedMeal', id: meal.id, name: meal.name, hint: 'shopping.readyMeal', meal },
    baseQuantity: quantity, displayQuantity: quantity, displayUnit: 'package', note,
  });

  beforeEach(() => {
    created = new Subject<ShoppingListItem>();
    shoppingListService = {
      getAll: vi.fn().mockReturnValue(of([])),
      create: vi.fn().mockReturnValue(created),
      createPreparedMeal: vi.fn().mockReturnValue(created),
      update: vi.fn(),
      generate: vi.fn().mockReturnValue(of([])),
      clearChecked: vi.fn().mockReturnValue(of({ removed: 0 })),
    };
    TestBed.configureTestingModule({
      providers: [
        ShoppingListPage,
        { provide: ShoppingListService, useValue: shoppingListService },
        { provide: IngredientService, useValue: { getAll: vi.fn().mockReturnValue(of([ingredient])) } },
        { provide: PreparedMealService, useValue: { list: vi.fn().mockReturnValue(of([meal])) } },
        { provide: TranslationService, useValue: { translate: (key: string) => key } },
      ],
    });
    page = TestBed.inject(ShoppingListPage);
    page.ngOnInit();
  });

  it('shows the product as a pending line while the save is still in flight', () => {
    page.add(addIngredient(200, ''));

    expect(page.pending().map(addition => [addition.name, addition.quantity, addition.unit]))
      .toEqual([['Pasta', 200, 'g']]);
    expect(page.items()).toEqual([]);
  });

  it('keeps a product in flight out of the picker so it cannot be queued twice', () => {
    page.add(addIngredient(200, ''));

    expect(page.availableIngredients()).toEqual([]);
  });

  it('keeps a ready meal in flight out of the picker too', () => {
    page.add(addMeal(2));

    expect(page.availablePreparedMeals()).toEqual([]);
  });

  it('holds bulk actions back while an add is still in flight', () => {
    page.add(addIngredient(200, ''));

    page.generate();
    page.clearChecked();

    expect(shoppingListService.generate).not.toHaveBeenCalled();
    expect(shoppingListService.clearChecked).not.toHaveBeenCalled();
  });

  it('swaps the pending line for the saved item once the API answers', () => {
    page.add(addIngredient(200, ''));
    created.next(item('pasta', 0));
    created.complete();

    expect(page.pending()).toEqual([]);
    expect(page.items().map(value => value.id)).toEqual(['pasta']);
  });

  it('clears the pending line and reports the failure when the save fails', () => {
    page.add(addIngredient(200, ''));
    created.error(new Error('offline'));

    expect(page.pending()).toEqual([]);
    expect(page.items()).toEqual([]);
    expect(page.error()).toBe('shopping.addError');
  });

  it('never blocks on a save, so a second product can be added right away', () => {
    page.add(addIngredient(200, ''));
    page.add(addMeal(2));

    expect(page.pending()).toHaveLength(2);
    expect(shoppingListService.createPreparedMeal).toHaveBeenCalledWith('gyoza', 2, null);
    expect(page.isSaving()).toBe(false);
  });

  it('passes a ready meal note through to the API', () => {
    page.add(addMeal(1, 'the spicy ones'));

    expect(shoppingListService.createPreparedMeal).toHaveBeenCalledWith('gyoza', 1, 'the spicy ones');
  });
});
