import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { ShoppingListItem } from '../../shopping-list.models';
import { ShoppingListService } from '../../shopping-list.service';
import { ShoppingListMutation, ShoppingListOfflineService, ShoppingListOfflineState, shoppingListCacheKey } from '../../shopping-list-offline.service';
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
    expect(shoppingListService.update).toHaveBeenCalledWith(expect.objectContaining({ id: 'pasta' }), { quantity: 1, isPurchased: false, note: 'organic' });
  });

  it('clears an existing note with an empty string', () => {
    page.updateNote(item('pasta', 0, 'old note'), '   ');
    expect(shoppingListService.update).toHaveBeenCalledWith(expect.objectContaining({ id: 'pasta' }), { quantity: 1, isPurchased: false, note: '' });
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

  it('reports a hidden ingredient as already on the list, with how much of it is down', () => {
    const page = loadPage([{ ...item('pasta', 0), quantity: 1500 }]);

    expect(page.listedProducts()).toEqual([{ id: 'pasta', name: 'pasta', quantity: 1.5, unit: 'kg' }]);
  });

  it('leaves a bought ingredient off the already-on-the-list answer, since it can be re-added', () => {
    const page = loadPage([boughtPasta]);

    expect(page.listedProducts()).toEqual([]);
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

  it('reports a product in flight as already on the list', () => {
    page.add(addIngredient(200, ''));

    expect(page.listedProducts()).toEqual([{ id: 'pasta', name: 'Pasta', quantity: 200, unit: 'g' }]);
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

describe('ShoppingListPage optimistic updates', () => {
  let page: ShoppingListPage;
  let updateResponse: Subject<ShoppingListItem>;
  let shoppingListService: {
    getAll: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn>;
    createPreparedMeal: ReturnType<typeof vi.fn>; update: ReturnType<typeof vi.fn>;
    generate: ReturnType<typeof vi.fn>; clearChecked: ReturnType<typeof vi.fn>;
  };
  const pasta = item('pasta', 0);

  beforeEach(() => {
    updateResponse = new Subject<ShoppingListItem>();
    shoppingListService = {
      getAll: vi.fn().mockReturnValue(of([pasta])),
      create: vi.fn(), createPreparedMeal: vi.fn(),
      update: vi.fn().mockReturnValue(updateResponse),
      generate: vi.fn().mockReturnValue(of([])), clearChecked: vi.fn().mockReturnValue(of({ removed: 0 })),
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

  it('shows a checkbox change immediately without blocking the row', () => {
    page.update(pasta, { isPurchased: true });

    expect(page.items()[0].isPurchased).toBe(true);
    expect(page.isItemUpdating('pasta')).toBe(true);
    expect(page.isSaving()).toBe(false);
    expect(shoppingListService.update).toHaveBeenCalledWith(expect.objectContaining({ id: 'pasta' }), {
      quantity: 1, isPurchased: true, note: null,
    });
  });

  it('uses the confirmed server response after a successful update', () => {
    page.update(pasta, { isPurchased: true });
    updateResponse.next({ ...pasta, isPurchased: true, totalPrice: 2 });

    expect(page.items()[0]).toMatchObject({ isPurchased: true, totalPrice: 2 });
    expect(page.isItemUpdating('pasta')).toBe(false);
  });

  it('restores the last confirmed value when an update fails', () => {
    page.update(pasta, { isPurchased: true });
    updateResponse.error(new Error('failed'));

    expect(page.items()[0]).toEqual(pasta);
    expect(page.isItemUpdating('pasta')).toBe(false);
    expect(page.error()).toBe('shopping.updateError');
  });

  it('serializes rapid toggles and sends the final selected state', () => {
    page.update(pasta, { isPurchased: true });
    page.update(page.items()[0], { isPurchased: false });

    expect(shoppingListService.update).toHaveBeenCalledTimes(1);
    updateResponse.next({ ...pasta, isPurchased: true });
    expect(shoppingListService.update).toHaveBeenCalledTimes(2);
    expect(shoppingListService.update).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'pasta' }), {
      quantity: 1, isPurchased: false, note: null,
    });

    updateResponse.next({ ...pasta, isPurchased: false });
    expect(page.items()[0].isPurchased).toBe(false);
    expect(page.isItemUpdating('pasta')).toBe(false);
  });
});

describe('ShoppingListPage offline shopping list', () => {
  let page: ShoppingListPage;
  let cacheState: ShoppingListOfflineState;
  let offlineStore: {
    load: ReturnType<typeof vi.fn>; saveSnapshot: ReturnType<typeof vi.fn>;
    queueMutation: ReturnType<typeof vi.fn>; removeMutation: ReturnType<typeof vi.fn>;
    markMutationFailed: ReturnType<typeof vi.fn>; clearUser: ReturnType<typeof vi.fn>;
  };
  let shoppingListService: {
    getAll: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn>;
    createPreparedMeal: ReturnType<typeof vi.fn>; update: ReturnType<typeof vi.fn>;
    generate: ReturnType<typeof vi.fn>; clearChecked: ReturnType<typeof vi.fn>;
  };
  const user = { id: 'user-1', activeHouseId: 'house-1' };
  const cachedPasta = { ...item('pasta', 0), isPurchased: false };
  const checkedPasta = { ...cachedPasta, isPurchased: true };
  const snapshot = () => ({ items: [cachedPasta], ingredients: [ingredient], preparedMeals: [], savedAt: Date.now() });
  const networkError = () => new HttpErrorResponse({ status: 0, statusText: 'Offline' });
  const permanentError = () => new HttpErrorResponse({ status: 500, statusText: 'Server error' });
  const waitForPage = () => new Promise<void>(resolve => setTimeout(resolve, 0));

  const mutation = (overrides: Partial<ShoppingListMutation> = {}): ShoppingListMutation => ({
    itemId: 'pasta', operationId: 'operation-1', quantity: 1, isPurchased: true, note: null,
    confirmed: cachedPasta, queuedAt: Date.now(), status: 'pending', ...overrides,
  });

  beforeEach(() => {
    cacheState = { snapshot: snapshot(), mutations: [] };
    offlineStore = {
      load: vi.fn().mockImplementation(async () => ({
        snapshot: cacheState.snapshot && { ...cacheState.snapshot, items: [...cacheState.snapshot.items] },
        mutations: cacheState.mutations.map(value => ({ ...value })),
      })),
      saveSnapshot: vi.fn().mockImplementation(async (_key: string, value: Omit<NonNullable<ShoppingListOfflineState['snapshot']>, 'savedAt'>) => {
        cacheState.snapshot = { ...value, savedAt: Date.now() };
      }),
      queueMutation: vi.fn().mockImplementation(async (_key: string, value: ShoppingListMutation) => {
        cacheState.mutations = [...cacheState.mutations.filter(current => current.itemId !== value.itemId), value];
      }),
      removeMutation: vi.fn().mockImplementation(async (_key: string, operationId: string) => {
        cacheState.mutations = cacheState.mutations.filter(value => value.operationId !== operationId);
      }),
      markMutationFailed: vi.fn().mockImplementation(async (_key: string, operationId: string, error: string) => {
        cacheState.mutations = cacheState.mutations.map(value => value.operationId === operationId ? { ...value, status: 'failed', error } : value);
      }),
      clearUser: vi.fn().mockResolvedValue(undefined),
    };
    shoppingListService = {
      getAll: vi.fn().mockReturnValue(throwError(networkError)),
      create: vi.fn(), createPreparedMeal: vi.fn(), update: vi.fn(),
      generate: vi.fn(), clearChecked: vi.fn(),
    };
    TestBed.configureTestingModule({
      providers: [
        ShoppingListPage,
        { provide: ShoppingListService, useValue: shoppingListService },
        { provide: ShoppingListOfflineService, useValue: offlineStore },
        { provide: IngredientService, useValue: { getAll: vi.fn().mockReturnValue(of([ingredient])) } },
        { provide: PreparedMealService, useValue: { list: vi.fn().mockReturnValue(of([])) } },
        { provide: TranslationService, useValue: { translate: (key: string) => key } },
        { provide: AuthService, useValue: { currentUser: signal(user) } },
      ],
    });
  });

  const loadPage = async (): Promise<ShoppingListPage> => {
    page = TestBed.inject(ShoppingListPage);
    page.ngOnInit();
    await waitForPage();
    return page;
  };

  it('scopes cache keys to both user and active household', () => {
    expect(shoppingListCacheKey(user)).toBe('user-1:house-1');
    expect(shoppingListCacheKey({ ...user, activeHouseId: 'house-2' })).not.toBe(shoppingListCacheKey(user));
    expect(shoppingListCacheKey({ ...user, id: 'user-2' })).not.toBe(shoppingListCacheKey(user));
  });

  it('renders the cached snapshot when the initial API request is unavailable', async () => {
    const loaded = await loadPage();

    expect(loaded.items()).toEqual([cachedPasta]);
    expect(loaded.syncState()).toBe('offline');
    expect(loaded.isLoading()).toBe(false);
  });

  it('restores a pending mutation after a page reload without losing the optimistic state', async () => {
    cacheState.mutations = [mutation()];
    const loaded = await loadPage();

    expect(loaded.items()[0].isPurchased).toBe(true);
    expect(loaded.pendingUpdateCount()).toBe(1);
    expect(loaded.syncState()).toBe('offline');
  });

  it('compacts rapid offline mutations to the final set-state command', async () => {
    const update = vi.fn().mockReturnValue(throwError(networkError));
    shoppingListService.update = update;
    const loaded = await loadPage();

    loaded.update(cachedPasta, { isPurchased: true });
    loaded.update(loaded.items()[0], { isPurchased: false });
    await waitForPage();

    expect(cacheState.mutations).toHaveLength(1);
    expect(cacheState.mutations[0]).toMatchObject({ itemId: 'pasta', isPurchased: false });
  });

  it('retries queued mutations on reconnection and refreshes the server snapshot', async () => {
    cacheState.mutations = [mutation()];
    shoppingListService.getAll
      .mockReturnValueOnce(throwError(networkError))
      .mockReturnValueOnce(of([checkedPasta]));
    const response = new Subject<ShoppingListItem>();
    shoppingListService.update.mockReturnValue(response);
    const loaded = await loadPage();

    loaded.onOnline();
    expect(shoppingListService.update).toHaveBeenCalledTimes(1);
    response.next(checkedPasta);
    await waitForPage();

    expect(loaded.items()).toEqual([checkedPasta]);
    expect(loaded.syncState()).toBe('synced');
    expect(cacheState.mutations).toEqual([]);
  });

  it('keeps a permanent failure visible and retries it explicitly', async () => {
    shoppingListService.getAll = vi.fn().mockReturnValue(of([cachedPasta]));
    shoppingListService.update
      .mockReturnValueOnce(throwError(permanentError))
      .mockReturnValueOnce(of(checkedPasta));
    const loaded = await loadPage();

    loaded.update(cachedPasta, { isPurchased: true });
    await waitForPage();
    expect(loaded.items()[0].isPurchased).toBe(false);
    expect(loaded.failedUpdateCount()).toBe(1);
    expect(loaded.syncState()).toBe('failed');

    loaded.retryFailed();
    await waitForPage();
    expect(loaded.items()[0].isPurchased).toBe(true);
    expect(loaded.failedUpdateCount()).toBe(0);
    expect(loaded.syncState()).toBe('synced');
  });
});
