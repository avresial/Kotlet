import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { foodCategories, Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { ShoppingAdd, ShoppingAddListedProduct, ShoppingAddRequest } from '../../components/shopping-add/shopping-add';
import { ShoppingListItem } from '../../shopping-list.models';
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
  const byCategory = (include: (item: ShoppingListItem) => boolean) => foodCategories
    .map(category => ({ key: `category-${category.value}`, label: category.label as string, isBought: false, items: items.filter(item => !item.preparedMealId && item.category === category.value && include(item)) }))
    .filter(group => group.items.length);
  const groups = (include: (item: ShoppingListItem) => boolean) => {
    const ingredientGroups = byCategory(include);
    const readyMeals = items.filter(item => !!item.preparedMealId && include(item));
    return readyMeals.length
      ? [...ingredientGroups, { key: 'ready-meals', label: 'shopping.readyMeals', isBought: false, items: readyMeals }]
      : ingredientGroups;
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
}

@Component({
  selector: 'app-shopping-list-page',
  imports: [RouterLink, ShoppingAdd, TranslatePipe],
  templateUrl: './shopping-list-page.html',
  styleUrl: './shopping-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShoppingListPage implements OnInit {
  private readonly shoppingListService = inject(ShoppingListService);
  private readonly ingredientService = inject(IngredientService);
  private readonly preparedMealService = inject(PreparedMealService);
  private readonly translations = inject(TranslationService);
  readonly items = signal<ShoppingListItem[]>([]);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly preparedMeals = signal<PreparedMeal[]>([]);
  readonly pending = signal<PendingAddition[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  private pendingCounter = 0;
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

  ngOnInit(): void {
    forkJoin({
      items: this.shoppingListService.getAll(),
      ingredients: this.ingredientService.getAll(),
      preparedMeals: this.preparedMealService.list(),
    })
      .pipe(finalize(() => this.isLoading.set(false))).subscribe({
        next: ({ items, ingredients, preparedMeals }) => {
          this.items.set(items); this.ingredients.set(ingredients); this.preparedMeals.set(preparedMeals);
        },
        error: error => this.error.set(getApiError(error, this.translations.translate('shopping.loadError'))),
      });
  }

  /** The add panel hands over and resets immediately; the save runs here in the background so the
      shopper can keep typing. Until the API answers, the line shows up as a pending row. */
  add(request: ShoppingAddRequest): void {
    this.error.set(null);
    const { option, baseQuantity, displayQuantity, displayUnit, note } = request;
    const ingredientId = option.kind === 'ingredient' ? option.id : null;
    const preparedMealId = option.kind === 'preparedMeal' ? option.id : null;
    const addition: PendingAddition = {
      key: `pending-${++this.pendingCounter}`, name: option.name,
      quantity: displayQuantity, unit: displayUnit, ingredientId, preparedMealId,
    };
    this.pending.update(additions => [...additions, addition]);
    // Re-adding something already bought restarts that line instead of stacking a duplicate: the tick,
    // the quantity and the note all reset to what was just entered rather than keeping the old shop's values.
    const purchased = this.items().find(item => item.isPurchased
      && (ingredientId ? item.ingredientId === ingredientId : item.preparedMealId === preparedMealId));
    const saveItem = purchased
      ? this.shoppingListService.update(purchased, { quantity: baseQuantity, isPurchased: false, note })
      : ingredientId
        ? this.shoppingListService.create(ingredientId, baseQuantity, note || null)
        : this.shoppingListService.createPreparedMeal(preparedMealId!, baseQuantity, note || null);
    saveItem
      .pipe(finalize(() => this.pending.update(additions => additions.filter(current => current.key !== addition.key))))
      .subscribe({
        next: item => this.items.update(items => items.some(current => current.id === item.id)
          ? items.map(current => current.id === item.id ? item : current)
          : [...items, item]),
        error: error => this.error.set(getApiError(error, this.translations.translate('shopping.addError'))),
      });
  }

  update(item: ShoppingListItem, changes: Partial<Pick<ShoppingListItem, 'quantity' | 'isPurchased' | 'note'>>): void {
    if (changes.quantity !== undefined && (!Number.isFinite(changes.quantity) || changes.quantity <= 0)) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.update(item, changes).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: updated => this.items.update(items => items.map(current => current.id === updated.id ? updated : current)),
      error: error => this.error.set(getApiError(error, this.translations.translate('shopping.updateError'))),
    });
  }

  remove(item: ShoppingListItem): void {
    if (this.isSaving()) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.delete(item.id).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => this.items.update(items => items.filter(current => current.id !== item.id)),
      error: error => this.error.set(getApiError(error, this.translations.translate('shopping.removeError'))),
    });
  }

  clearChecked(): void {
    // A bulk action while an add is still in flight would fight over the same list, so both wait.
    if (this.isSaving() || this.pending().length || this.purchasedCount() === 0) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.clearChecked().pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => this.items.update(items => items.filter(item => !item.isPurchased)),
      error: error => this.error.set(getApiError(error, this.translations.translate('shopping.clearError'))),
    });
  }

  generate(): void {
    if (this.isSaving() || this.pending().length || this.generateTo() < this.generateFrom()) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.generate(this.generateFrom(), this.generateTo()).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: items => this.items.set(items),
      error: error => this.error.set(getApiError(error, this.translations.translate('shopping.generateError'))),
    });
  }

  unitLabel(unit: DisplayUnit | 'package'): string { return shortUnitLabel(unit); }
  display(item: ShoppingListItem) {
    if (item.preparedMealId) return { quantity: item.quantity, unit: 'package' as const };
    const ingredient = this.ingredients().find(value => value.id === item.ingredientId);
    return ingredient ? displayMeasurement(item.quantity, ingredient) : { quantity: item.quantity, unit: item.measurementUnit as DisplayUnit };
  }
  updateDisplayQuantity(item: ShoppingListItem, quantity: number): void {
    if (item.preparedMealId) { this.update(item, { quantity }); return; }
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
