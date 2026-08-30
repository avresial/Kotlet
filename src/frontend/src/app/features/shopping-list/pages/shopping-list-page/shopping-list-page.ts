import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, HostListener, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { foodCategories, Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { ShoppingAdd, ShoppingAddListedProduct, ShoppingAddRequest } from '../../components/shopping-add/shopping-add';
import { ShoppingListItem, ShoppingListUpdate } from '../../shopping-list.models';
import { ShoppingListMutation, ShoppingListOfflineService, ShoppingListOfflineState, shoppingListCacheKey } from '../../shopping-list-offline.service';
import { ShoppingListService } from '../../shopping-list.service';
import { DisplayUnit, displayMeasurement, shortUnitLabel, toBaseQuantity } from '../../../ingredients/display-units';
import { PreparedMeal } from '../../../prepared-meals/prepared-meal.models';
import { PreparedMealService } from '../../../prepared-meals/prepared-meal.service';

export interface ShoppingListGroup {
  key: string;
  label: string;
  items: ShoppingListItem[];
  isBought: boolean;
}

/** Items still to buy stay grouped by category at the top; everything already ticked off drops into a
    single group at the bottom, so the list always opens on what is left to pick up. */
export function groupShoppingItems(items: ShoppingListItem[]): ShoppingListGroup[] {
  const isCustom = (item: ShoppingListItem) => !item.ingredientId && !item.preparedMealId;
  const isReadyMeal = (item: ShoppingListItem) => !!item.preparedMealId;
  const isIngredient = (item: ShoppingListItem) => !isCustom(item) && !isReadyMeal(item);

  const byCategory = (include: (item: ShoppingListItem) => boolean) => foodCategories
    .map(category => ({
      key: `category-${category.value}`,
      label: category.label as string,
      isBought: false,
      items: items.filter(item => isIngredient(item) && item.category === category.value && include(item)),
    }))
    .filter(group => group.items.length);

  const groups = (include: (item: ShoppingListItem) => boolean) => {
    const ingredientGroups = byCategory(include);
    const readyMeals = items.filter(item => isReadyMeal(item) && include(item));
    const customItems = items.filter(item => isCustom(item) && include(item));
    const result = [...ingredientGroups];
    if (readyMeals.length) {
      result.push({ key: 'ready-meals', label: 'shopping.readyMeals', isBought: false, items: readyMeals });
    }
    if (customItems.length) {
      result.push({ key: 'custom-items', label: 'shopping.customItems', isBought: false, items: customItems });
    }
    return result;
  };

  const remaining = groups(item => !item.isPurchased);
  const bought = groups(item => item.isPurchased).flatMap(group => group.items);
  return bought.length ? [...remaining, { key: 'bought', label: 'shopping.alreadyBought', isBought: true, items: bought }] : remaining;
}

/** A line the shopper has already handed over, still waiting for the API to confirm it. */
export interface PendingAddition {
  key: string;
  name: string;
  quantity: number;
  unit: DisplayUnit | 'package';
  ingredientId: string | null;
  preparedMealId: string | null;
  customName?: string | null;
}

interface PendingUpdate {
  confirmed: ShoppingListItem | null;
  desired: ShoppingListUpdate;
  operationId: string;
  status: 'pending' | 'failed';
  inFlight: boolean;
  persisted: boolean;
}

type ShoppingListSyncState = 'loading' | 'synced' | 'offline' | 'syncing' | 'failed';

@Component({
  selector: 'app-shopping-list-page',
  imports: [RouterLink, ShoppingAdd, TranslatePipe],
  templateUrl: './shopping-list-page.html',
  styleUrl: './shopping-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShoppingListPage implements OnInit {
  private readonly auth = inject(AuthService, { optional: true });
  private readonly shoppingListService = inject(ShoppingListService);
  private readonly offline = inject(ShoppingListOfflineService);
  private readonly ingredientService = inject(IngredientService);
  private readonly preparedMealService = inject(PreparedMealService);
  private readonly translations = inject(TranslationService);
  readonly items = signal<ShoppingListItem[]>([]);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly preparedMeals = signal<PreparedMeal[]>([]);
  readonly pending = signal<PendingAddition[]>([]);
  private readonly pendingUpdates = signal(new Map<string, PendingUpdate>());
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly syncState = signal<ShoppingListSyncState>('loading');
  private pendingCounter = 0;
  private cacheKey: string | null = null;
  private loaded = false;
  private flushingQueue = false;
  readonly generateFrom = signal(this.dateString(this.monday(new Date())));
  readonly generateTo = signal(this.dateString(new Date(this.monday(new Date()).getTime() + 6 * 86400000)));
  readonly showGenerate = signal(false);
  readonly error = signal<string | null>(null);
  /** Something already ticked off stays pickable so the shopper can restart that line — see add().
      Anything still in flight is held back so a fast typist cannot queue the same product twice. */
  readonly availableIngredients = computed(() => this.ingredients().filter(ingredient =>
    !this.items().some(item => item.ingredientId === ingredient.id && !item.isPurchased)
    && !this.pending().some(addition => addition.ingredientId === ingredient.id)));
  readonly availablePreparedMeals = computed(() => this.preparedMeals().filter(meal =>
    !this.items().some(item => item.preparedMealId === meal.id && !item.isPurchased)
    && !this.pending().some(addition => addition.preparedMealId === meal.id)));
  /** The other half of the filtering above: what was held back, and how much of it is already
      down, so searching for it again gets an answer instead of "no matching products". */
  readonly listedProducts = computed<ShoppingAddListedProduct[]>(() => {
    // Keyed by product so a line that is on the list and in flight at once is only listed once.
    const listed = new Map<string, ShoppingAddListedProduct>();
    const put = (id: string, name: string, quantity: number, unit: DisplayUnit | 'package') => listed.set(id, { id, name, quantity, unit });
    for (const item of this.items())
      if (!item.isPurchased) {
        const measure = this.display(item);
        put(item.preparedMealId ?? item.ingredientId ?? item.id, item.ingredientName, measure.quantity, measure.unit);
      }
    for (const addition of this.pending())
      put(addition.preparedMealId ?? addition.ingredientId ?? addition.key, addition.name, addition.quantity, addition.unit);
    return [...listed.values()];
  });
  readonly purchasedCount = computed(() => this.items().filter(item => item.isPurchased).length);
  readonly totalPrice = computed(() => this.items().reduce((sum, item) => sum + item.totalPrice, 0));
  readonly groups = computed(() => groupShoppingItems(this.items()));
  readonly pendingUpdateCount = computed(() => [...this.pendingUpdates().values()].filter(update => update.status === 'pending').length);
  readonly failedUpdateCount = computed(() => [...this.pendingUpdates().values()].filter(update => update.status === 'failed').length);
  readonly syncLabel = computed(() => ({
    loading: 'shopping.loading', synced: 'shopping.syncSynced', offline: 'shopping.syncOffline',
    syncing: 'shopping.syncing', failed: 'shopping.syncFailed',
  })[this.syncState()]);

  ngOnInit(): void {
    this.cacheKey = shoppingListCacheKey(this.auth?.currentUser() ?? null);
    if (!this.cacheKey) return this.loadOnline(null);
    void this.loadCachedThenOnline();
  }

  private async loadCachedThenOnline(): Promise<void> {
    let cached: ShoppingListOfflineState | null = null;
    try {
      cached = await this.offline.load(this.cacheKey!);
    } catch {
      // A storage-restricted browser can still use the online list.
    }
    if (cached) this.restoreCachedState(cached);
    this.loadOnline(cached);
  }

  private loadOnline(cached: ShoppingListOfflineState | null): void {
    forkJoin({
      items: this.shoppingListService.getAll(),
      ingredients: this.ingredientService.getAll(),
      preparedMeals: this.preparedMealService.list(),
    })
      .pipe(finalize(() => this.isLoading.set(false))).subscribe({
        next: result => this.applyOnlineSnapshot(result),
        error: error => this.handleLoadError(error, cached),
      });
  }

  private restoreCachedState(state: ShoppingListOfflineState): void {
    if (state.snapshot) {
      this.ingredients.set(state.snapshot.ingredients);
      this.preparedMeals.set(state.snapshot.preparedMeals);
      this.items.set(this.applyPendingUpdates(state.snapshot.items, state.mutations));
      this.syncState.set(this.networkAvailable() ? 'syncing' : 'offline');
    }
    this.pendingUpdates.set(new Map(state.mutations.map(mutation => [mutation.itemId, {
      confirmed: mutation.confirmed ?? state.snapshot?.items.find(item => item.id === mutation.itemId) ?? null,
      desired: this.updateFromMutation(mutation), operationId: mutation.operationId,
      status: mutation.status ?? 'pending', inFlight: false, persisted: true,
    }])));
    if (this.failedUpdateCount()) this.syncState.set('failed');
  }

  private applyOnlineSnapshot(result: {
    items: ShoppingListItem[];
    ingredients: Ingredient[];
    preparedMeals: PreparedMeal[];
  }): void {
    this.ingredients.set(result.ingredients);
    this.preparedMeals.set(result.preparedMeals);
    this.pendingUpdates.update(updates => {
      const next = new Map(updates);
      for (const [id, pending] of next) {
        const confirmed = result.items.find(item => item.id === id);
        if (confirmed) next.set(id, { ...pending, confirmed });
      }
      return next;
    });
    this.items.set(this.overlayPendingUpdates(result.items));
    this.loaded = true;
    this.error.set(null);
    this.syncState.set(this.failedUpdateCount() ? 'failed' : this.pendingUpdateCount() ? 'syncing' : 'synced');
    this.persistSnapshot();
    if (this.pendingUpdateCount()) this.flushQueued();
  }

  private handleLoadError(error: unknown, cached: ShoppingListOfflineState | null): void {
    this.loaded = true;
    const networkFailure = this.isNetworkFailure(error);
    if (cached?.snapshot) {
      this.syncState.set(networkFailure ? 'offline' : 'failed');
      if (!networkFailure) this.error.set(getApiError(error, this.translations.translate('shopping.loadError')));
      return;
    }
    this.syncState.set(networkFailure ? 'offline' : 'failed');
    this.error.set(getApiError(error, this.translations.translate('shopping.loadError')));
  }

  @HostListener('window:online')
  onOnline(): void {
    if (!this.loaded) return;
    this.syncState.set('syncing');
    this.flushQueued();
  }

  @HostListener('window:offline')
  onOffline(): void {
    if (this.loaded) this.syncState.set('offline');
  }

  private applyPendingUpdates(items: ShoppingListItem[], mutations: ShoppingListMutation[]): ShoppingListItem[] {
    return mutations.reduce((current, mutation) => mutation.status === 'pending'
      ? current.map(item => item.id === mutation.itemId ? this.optimisticItem(item, this.updateFromMutation(mutation)) : item)
      : current, items);
  }

  private overlayPendingUpdates(items: ShoppingListItem[]): ShoppingListItem[] {
    return items.map(item => {
      const pending = this.pendingUpdates().get(item.id);
      return pending?.status === 'pending' ? this.optimisticItem(item, pending.desired) : item;
    });
  }

  private updateFromMutation(mutation: ShoppingListMutation): ShoppingListUpdate {
    return { quantity: mutation.quantity, isPurchased: mutation.isPurchased, note: mutation.note };
  }

  private persistSnapshot(): void {
    if (!this.cacheKey) return;
    void this.offline.saveSnapshot(this.cacheKey, {
      items: this.items(), ingredients: this.ingredients(), preparedMeals: this.preparedMeals(),
    }).catch(() => undefined);
  }

  private networkAvailable(): boolean {
    return typeof navigator === 'undefined' || navigator.onLine !== false;
  }

  private isNetworkFailure(error: unknown): boolean {
    return error instanceof HttpErrorResponse && error.status === 0;
  }

  /** The add panel hands over and resets immediately; the save runs here in the background so the
      shopper can keep typing. Until the API answers, the line shows up as a pending row. */
  add(request: ShoppingAddRequest): void {
    this.error.set(null);
    const { option, baseQuantity, displayQuantity, displayUnit, note } = request;
    const ingredientId = option.kind === 'ingredient' ? option.id : null;
    const preparedMealId = option.kind === 'preparedMeal' ? option.id : null;
    const customName = option.kind === 'custom' ? option.name : null;
    const addition: PendingAddition = {
      key: `pending-${++this.pendingCounter}`, name: option.name,
      quantity: displayQuantity, unit: displayUnit, ingredientId, preparedMealId, customName,
    };
    this.pending.update(additions => [...additions, addition]);
    // Re-adding something already bought restarts that line instead of stacking a duplicate: the tick,
    // the quantity and the note all reset to what was just entered rather than keeping the old shop's values.
    const purchased = this.items().find(item => item.isPurchased
      && (ingredientId
        ? item.ingredientId === ingredientId
        : preparedMealId
          ? item.preparedMealId === preparedMealId
          : (item.customName ?? item.ingredientName)?.toLowerCase() === customName?.toLowerCase()));
    const saveItem = purchased
      ? this.shoppingListService.update(purchased, { quantity: baseQuantity, isPurchased: false, note })
      : ingredientId
        ? this.shoppingListService.create(ingredientId, baseQuantity, note || null)
        : preparedMealId
          ? this.shoppingListService.createPreparedMeal(preparedMealId, baseQuantity, note || null)
          : this.shoppingListService.createCustom(customName!, baseQuantity, note || null);
    saveItem
      .pipe(finalize(() => this.pending.update(additions => additions.filter(current => current.key !== addition.key))))
      .subscribe({
        next: item => this.items.update(items => items.some(current => current.id === item.id)
          ? items.map(current => current.id === item.id ? item : current)
          : [...items, item]),
        error: error => this.error.set(getApiError(error, this.translations.translate('shopping.addError'))),
      });
  }

  update(item: ShoppingListItem, changes: Partial<ShoppingListUpdate>): void {
    if (this.isSaving()) return;
    if (changes.quantity !== undefined && (!Number.isFinite(changes.quantity) || changes.quantity <= 0)) return;
    const current = this.items().find(value => value.id === item.id) ?? item;
    const pending = this.pendingUpdates().get(item.id);
    const desired: ShoppingListUpdate = {
      quantity: changes.quantity ?? current.quantity,
      isPurchased: changes.isPurchased ?? current.isPurchased,
      note: changes.note !== undefined ? changes.note : current.note,
    };
    this.error.set(null);
    this.items.update(items => items.map(value => value.id === item.id ? this.optimisticItem(value, desired) : value));
    const operationId = this.newOperationId();
    const nextPending: PendingUpdate = {
      confirmed: pending?.confirmed ?? current, desired, operationId,
      status: 'pending', inFlight: pending?.inFlight ?? false, persisted: !this.cacheKey,
    };
    this.pendingUpdates.update(updates => {
      const next = new Map(updates);
      next.set(item.id, nextPending);
      return next;
    });
    this.syncState.set(this.networkAvailable() ? 'syncing' : 'offline');
    this.persistMutation(item.id, nextPending);
  }

  isItemUpdating(id: string): boolean { return this.pendingUpdates().get(id)?.status === 'pending'; }

  private startUpdate(id: string): void {
    const pending = this.pendingUpdates().get(id);
    if (!pending || pending.status !== 'pending' || pending.inFlight || !pending.persisted) return;
    if (!this.networkAvailable()) {
      this.syncState.set('offline');
      return;
    }
    this.setPendingUpdate(id, { ...pending, inFlight: true });
    const requestItem = this.items().find(item => item.id === id) ?? pending.confirmed ?? { id } as ShoppingListItem;
    this.shoppingListService.update(requestItem, pending.desired).subscribe({
      next: updated => {
        const current = this.pendingUpdates().get(id);
        if (!current) return;
        if (this.sameUpdate(current.desired, pending.desired)) {
          this.pendingUpdates.update(updates => {
            const next = new Map(updates);
            next.delete(id);
            return next;
          });
          this.items.update(items => items.some(item => item.id === updated.id)
            ? items.map(item => item.id === updated.id ? updated : item) : [...items, updated]);
          void this.removePersistedMutation(current.operationId);
          if (this.flushingQueue) {
            if (this.pendingUpdateCount() === 0) this.refreshAfterSync();
          } else {
            this.syncState.set(this.failedUpdateCount() ? 'failed' : this.pendingUpdateCount() ? 'syncing' : 'synced');
            this.persistSnapshot();
          }
          return;
        }
        this.setPendingUpdate(id, { ...current, confirmed: updated, inFlight: false });
        this.items.update(items => items.some(item => item.id === updated.id)
          ? items.map(item => item.id === updated.id ? this.optimisticItem(updated, current.desired) : item)
          : [...items, this.optimisticItem(updated, current.desired)]);
        this.startUpdate(id);
      },
      error: error => {
        const current = this.pendingUpdates().get(id);
        if (!current) return;
        if (this.isNetworkFailure(error)) {
          this.setPendingUpdate(id, { ...current, inFlight: false });
          this.syncState.set('offline');
          this.persistSnapshot();
          return;
        }
        if (!this.sameUpdate(current.desired, pending.desired)) {
          this.setPendingUpdate(id, { ...current, inFlight: false });
          this.items.update(items => items.map(item => item.id === id ? this.optimisticItem(current.confirmed ?? item, current.desired) : item));
          this.startUpdate(id);
          return;
        }
        if (current.confirmed) this.items.update(items => items.map(item => item.id === id ? current.confirmed! : item));
        this.setPendingUpdate(id, { ...current, status: 'failed', inFlight: false, persisted: true });
        if (this.cacheKey) void this.offline.markMutationFailed(this.cacheKey, current.operationId, 'shopping.updateError').catch(() => undefined);
        this.syncState.set('failed');
        this.persistSnapshot();
        if (this.flushingQueue && this.pendingUpdateCount() === 0) this.flushingQueue = false;
        this.error.set(getApiError(error, this.translations.translate('shopping.updateError')));
      },
    });
  }

  private persistMutation(id: string, pending: PendingUpdate): void {
    if (!this.cacheKey) {
      this.startUpdate(id);
      return;
    }
    const mutation: ShoppingListMutation = {
      ...pending.desired, operationId: pending.operationId, itemId: id,
      confirmed: pending.confirmed, queuedAt: Date.now(), status: 'pending',
    };
    void this.offline.queueMutation(this.cacheKey, mutation).then(() => {
      const current = this.pendingUpdates().get(id);
      if (!current || current.operationId !== pending.operationId) return;
      this.setPendingUpdate(id, { ...current, persisted: true });
      this.persistSnapshot();
      this.startUpdate(id);
    }).catch(() => {
      // Keep the optimistic interaction usable online even if a browser blocks IndexedDB.
      const current = this.pendingUpdates().get(id);
      if (!current || current.operationId !== pending.operationId) return;
      this.setPendingUpdate(id, { ...current, persisted: true });
      this.syncState.set(this.networkAvailable() ? 'syncing' : 'offline');
      this.startUpdate(id);
    });
  }

  private removePersistedMutation(operationId: string): Promise<void> {
    return this.cacheKey ? this.offline.removeMutation(this.cacheKey, operationId).catch(() => undefined) : Promise.resolve();
  }

  private flushQueued(): void {
    if (!this.pendingUpdateCount()) {
      this.flushingQueue = false;
      this.syncState.set(this.failedUpdateCount() ? 'failed' : 'synced');
      return;
    }
    if (!this.networkAvailable()) {
      this.syncState.set('offline');
      return;
    }
    this.flushingQueue = true;
    for (const id of this.pendingUpdates().keys()) this.startUpdate(id);
  }

  retryFailed(): void {
    const failed = [...this.pendingUpdates().entries()].filter(([, update]) => update.status === 'failed');
    if (!failed.length) return;
    this.error.set(null);
    for (const [id, update] of failed) {
      const retried = { ...update, status: 'pending' as const, inFlight: false, persisted: !this.cacheKey };
      this.setPendingUpdate(id, retried);
      this.items.update(items => items.map(item => item.id === id ? this.optimisticItem(item, update.desired) : item));
      this.persistMutation(id, retried);
    }
    this.syncState.set(this.networkAvailable() ? 'syncing' : 'offline');
  }

  private refreshAfterSync(): void {
    this.flushingQueue = false;
    this.shoppingListService.getAll().subscribe({
      next: items => {
        this.items.set(this.overlayPendingUpdates(items));
        this.syncState.set(this.failedUpdateCount() ? 'failed' : this.pendingUpdateCount() ? 'syncing' : 'synced');
        this.persistSnapshot();
        if (this.pendingUpdateCount()) this.flushQueued();
      },
      error: error => {
        this.syncState.set(this.isNetworkFailure(error) ? 'offline' : 'failed');
        if (!this.isNetworkFailure(error)) this.error.set(getApiError(error, this.translations.translate('shopping.loadError')));
      },
    });
  }

  private newOperationId(): string {
    return globalThis.crypto?.randomUUID?.() ?? `shopping-${Date.now()}-${++this.pendingCounter}`;
  }

  private setPendingUpdate(id: string, value: PendingUpdate): void {
    this.pendingUpdates.update(updates => new Map(updates).set(id, value));
  }

  private sameUpdate(left: ShoppingListUpdate, right: ShoppingListUpdate): boolean {
    return left.quantity === right.quantity && left.isPurchased === right.isPurchased && left.note === right.note;
  }

  private optimisticItem(item: ShoppingListItem, update: ShoppingListUpdate): ShoppingListItem {
    const isPackage = !!item.preparedMealId || (!item.ingredientId && !item.preparedMealId);
    const unitPrice = item.pricePer100BaseUnits ?? 0;
    const totalPrice = isPackage
      ? update.quantity * unitPrice
      : (update.quantity / 100) * unitPrice;
    return { ...item, ...update, totalPrice: this.roundToCents(totalPrice) };
  }

  private roundToCents(value: number): number {
    const cents = value * 100;
    const lower = Math.floor(cents);
    const fraction = cents - lower;
    const rounded = fraction > .5 || (Math.abs(fraction - .5) < 1e-9 && lower % 2 !== 0)
      ? lower + 1
      : lower;
    return rounded / 100;
  }

  remove(item: ShoppingListItem): void {
    if (this.isSaving() || this.isItemUpdating(item.id)) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.delete(item.id).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => this.items.update(items => items.filter(current => current.id !== item.id)),
      error: error => this.error.set(getApiError(error, this.translations.translate('shopping.removeError'))),
    });
  }

  clearChecked(): void {
    // A bulk action while an add is still in flight would fight over the same list, so both wait.
    if (this.isSaving() || this.pending().length || this.pendingUpdateCount() || this.failedUpdateCount() || this.purchasedCount() === 0) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.clearChecked().pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => this.items.update(items => items.filter(item => !item.isPurchased)),
      error: error => this.error.set(getApiError(error, this.translations.translate('shopping.clearError'))),
    });
  }

  generate(): void {
    if (this.isSaving() || this.pending().length || this.pendingUpdateCount() || this.failedUpdateCount() || this.generateTo() < this.generateFrom()) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.generate(this.generateFrom(), this.generateTo()).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: items => this.items.set(items),
      error: error => this.error.set(getApiError(error, this.translations.translate('shopping.generateError'))),
    });
  }

  unitLabel(unit: DisplayUnit | 'package'): string { return shortUnitLabel(unit); }
  display(item: ShoppingListItem) {
    if (item.preparedMealId || (!item.ingredientId && !item.preparedMealId)) return { quantity: item.quantity, unit: 'package' as const };
    const ingredient = this.ingredients().find(value => value.id === item.ingredientId);
    return ingredient ? displayMeasurement(item.quantity, ingredient) : { quantity: item.quantity, unit: (item.measurementUnit || 'package') as DisplayUnit };
  }
  updateDisplayQuantity(item: ShoppingListItem, quantity: number): void {
    if (item.preparedMealId || (!item.ingredientId && !item.preparedMealId)) { this.update(item, { quantity }); return; }
    const ingredient = this.ingredients().find(value => value.id === item.ingredientId);
    if (ingredient) this.update(item, { quantity: toBaseQuantity(quantity, this.display(item).unit as DisplayUnit, ingredient) });
  }
  updateNote(item: ShoppingListItem, note: string): void {
    // Send the (possibly empty) trimmed string so the backend clears the note on blank
    // rather than treating an omitted note as "no change".
    const trimmed = note.trim();
    if (trimmed === (item.note ?? '')) return;
    this.update(item, { note: trimmed });
  }

  print(): void { window.print(); }

  private monday(date: Date): Date {
    date.setDate(date.getDate() - (date.getDay() + 6) % 7);
    return date;
  }
  private dateString(date: Date): string {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  }
}
