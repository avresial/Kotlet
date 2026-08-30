import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, input, output, signal, viewChild } from '@angular/core';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { DisplayUnit, shortUnitLabel, toBaseQuantity, unitsForIngredient } from '../../../ingredients/display-units';
import { PreparedMeal } from '../../../prepared-meals/prepared-meal.models';

/** A searchable product: either a catalogue ingredient, a ready meal, or a custom free-text item. */
export type ShoppingAddOption =
  | { kind: 'ingredient'; id: string; name: string; hint: string; ingredient: Ingredient }
  | { kind: 'preparedMeal'; id: string; name: string; hint: string; meal: PreparedMeal }
  | { kind: 'custom'; name: string; hint: string };

/** A product the page keeps out of the pickable results because it is already waiting on the
    list — searched all the same, so a repeat search says how much is there. */
export interface ShoppingAddListedProduct {
  id: string;
  name: string;
  quantity: number;
  unit: DisplayUnit | 'package';
}

/** What the page has to save: a quantity already converted to the base unit the API stores. */
export interface ShoppingAddRequest {
  option: ShoppingAddOption;
  /** Grams/millilitres for ingredients, packages for ready meals and custom items. */
  baseQuantity: number;
  /** What the shopper typed, kept for the pending line while the save is in flight. */
  displayQuantity: number;
  displayUnit: DisplayUnit | 'package';
  note: string;
}

const packageUnit = 'package' as const;

/** Steps the whole row up in shop-sized amounts rather than by 1 g, which nobody buys. */
function stepFor(unit: DisplayUnit | typeof packageUnit): number {
  return unit === 'g' || unit === 'ml' ? 50 : unit === 'kg' || unit === 'l' ? 0.5 : 1;
}

/** One ranking for both result lists: prefix matches first, the rest alphabetical. */
function matching<T extends { name: string }>(products: readonly T[], query: string, limit: number): T[] {
  return products
    .filter(product => product.name.toLocaleLowerCase().includes(query))
    .sort((a, b) => {
      const aStarts = a.name.toLocaleLowerCase().startsWith(query);
      const bStarts = b.name.toLocaleLowerCase().startsWith(query);
      return Number(bStarts) - Number(aStarts) || a.name.localeCompare(b.name);
    })
    .slice(0, limit);
}

@Component({
  selector: 'app-shopping-add',
  imports: [TranslatePipe],
  templateUrl: './shopping-add.html',
  styleUrl: './shopping-add.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShoppingAdd {
  readonly ingredients = input.required<readonly Ingredient[]>();
  readonly preparedMeals = input.required<readonly PreparedMeal[]>();
  readonly onList = input<readonly ShoppingAddListedProduct[]>([]);
  readonly add = output<ShoppingAddRequest>();

  readonly query = signal('');
  readonly selected = signal<ShoppingAddOption | null>(null);
  readonly activeIndex = signal(0);
  readonly quantity = signal(1);
  readonly unit = signal<DisplayUnit | typeof packageUnit>('g');
  readonly note = signal('');
  readonly showNote = signal(false);

  private readonly searchBox = viewChild<ElementRef<HTMLInputElement>>('search');
  private readonly quantityBox = viewChild<ElementRef<HTMLInputElement>>('quantityInput');
  private readonly refocusSearch = signal(false);

  private readonly options = computed<ShoppingAddOption[]>(() => [
    ...this.ingredients().map((ingredient): ShoppingAddOption => ({
      kind: 'ingredient', id: ingredient.id, name: ingredient.name, hint: ingredient.measurementUnit, ingredient,
    })),
    ...this.preparedMeals().map((meal): ShoppingAddOption => ({
      kind: 'preparedMeal', id: meal.id, name: meal.brand ? `${meal.name} — ${meal.brand}` : meal.name,
      hint: 'shopping.readyMeal', meal,
    })),
  ]);

  /** Ingredients and ready meals share one result list, best prefix matches first. */
  readonly suggestions = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    return query ? matching(this.options(), query, 8) : [];
  });

  /** Matches among what is already on the list — shown under the pickable results so a product
      the page holds back is answered with "already there", never with "no matching products". */
  readonly listedMatches = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    return query ? matching(this.onList(), query, 4) : [];
  });

  readonly customOption = computed<ShoppingAddOption | null>(() => {
    const query = this.query().trim();
    return query ? { kind: 'custom', name: query, hint: 'shopping.customItem' } : null;
  });

  readonly units = computed<(DisplayUnit | typeof packageUnit)[]>(() => {
    const option = this.selected();
    if (!option) return [];
    return option.kind === 'ingredient' ? unitsForIngredient(option.ingredient) : [packageUnit];
  });
  readonly canSubmit = computed(() => !!this.selected() && Number.isFinite(this.quantity()) && this.quantity() > 0);

  constructor() {
    // Landing straight in the quantity box means a pick can be finished without reaching for the mouse.
    effect(() => {
      const quantity = this.quantityBox()?.nativeElement;
      if (!quantity) return;
      quantity.focus();
      quantity.select();
    });
    effect(() => {
      const search = this.searchBox();
      if (search && this.refocusSearch()) {
        this.refocusSearch.set(false);
        search.nativeElement.focus();
      }
    });
  }

  onInput(value: string): void {
    this.query.set(value);
    this.activeIndex.set(0);
  }

  select(option: ShoppingAddOption): void {
    this.selected.set(option);
    this.query.set('');
    this.activeIndex.set(0);
    this.unit.set(option.kind === 'ingredient' ? option.ingredient.measurementUnit as DisplayUnit : packageUnit);
    this.quantity.set(1);
    this.note.set('');
    this.showNote.set(false);
  }

  clearSelection(): void {
    this.selected.set(null);
    this.refocusSearch.set(true);
  }

  setUnit(unit: DisplayUnit | typeof packageUnit): void { this.unit.set(unit); }
  setQuantity(value: number): void { this.quantity.set(Number.isFinite(value) ? value : 0); }
  step(direction: 1 | -1): void {
    const step = stepFor(this.unit());
    const next = Math.round((this.quantity() + direction * step) * 1000) / 1000;
    // One step is the floor, except below it, where the floor is what is already there — otherwise
    // the minus button would raise a hand-typed 20 g up to 50 g.
    this.quantity.set(Math.max(next, Math.min(step, this.quantity())));
  }

  onSearchKeydown(event: KeyboardEvent): void {
    const suggestions = this.suggestions();
    if (event.key === 'Escape') { this.query.set(''); return; }
    if (event.key === 'Enter' && !suggestions.length && this.query().trim()) {
      event.preventDefault();
      const custom = this.customOption();
      if (custom) this.select(custom);
      return;
    }
    if (!suggestions.length) return;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeIndex.update(index => Math.min(index + 1, suggestions.length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeIndex.update(index => Math.max(index - 1, 0));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.select(suggestions[this.activeIndex()]);
    }
  }

  /** Hands the request over and resets straight away — the save runs in the background so the
      next product can be typed while the previous one is still on its way to the API. */
  submit(): void {
    const option = this.selected();
    if (!option || !this.canSubmit()) return;
    const unit = this.unit();
    const quantity = this.quantity();
    this.add.emit({
      option,
      baseQuantity: option.kind === 'ingredient' ? toBaseQuantity(quantity, unit as DisplayUnit, option.ingredient) : quantity,
      displayQuantity: quantity,
      displayUnit: unit,
      note: this.note().trim(),
    });
    this.selected.set(null);
    this.note.set('');
    this.showNote.set(false);
    this.refocusSearch.set(true);
  }

  unitLabel(unit: DisplayUnit | typeof packageUnit): string { return shortUnitLabel(unit); }
}
