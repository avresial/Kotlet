import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { foodCategories, Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { IngredientPicker } from '../../../ingredients/components/ingredient-picker/ingredient-picker';
import { ShoppingListItem } from '../../shopping-list.models';
import { ShoppingListService } from '../../shopping-list.service';
import { DisplayUnit, displayMeasurement, toBaseQuantity, unitsForIngredient } from '../../../ingredients/display-units';

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
    .map(category => ({ key: `category-${category.value}`, label: category.label as string, isBought: false, items: items.filter(item => item.category === category.value && include(item)) }))
    .filter(group => group.items.length);
  const remaining = byCategory(item => !item.isPurchased);
  const bought = byCategory(item => item.isPurchased).flatMap(group => group.items);
  return bought.length ? [...remaining, { key: 'bought', label: 'shopping.alreadyBought', isBought: true, items: bought }] : remaining;
}

@Component({
  selector: 'app-shopping-list-page',
  imports: [ReactiveFormsModule, RouterLink, IngredientPicker, TranslatePipe],
  templateUrl: './shopping-list-page.html',
  styleUrl: './shopping-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShoppingListPage implements OnInit {
  private readonly shoppingListService = inject(ShoppingListService);
  private readonly ingredientService = inject(IngredientService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly translations = inject(TranslationService);
  readonly items = signal<ShoppingListItem[]>([]);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly generateFrom = signal(this.dateString(this.monday(new Date())));
  readonly generateTo = signal(this.dateString(new Date(this.monday(new Date()).getTime() + 6 * 86400000)));
  readonly showGenerate = signal(false);
  readonly error = signal<string | null>(null);
  /** Something already ticked off stays pickable so the shopper can restart that line — see add(). */
  readonly availableIngredients = computed(() => this.ingredients().filter(ingredient =>
    !this.items().some(item => item.ingredientId === ingredient.id && !item.isPurchased)));
  readonly purchasedCount = computed(() => this.items().filter(item => item.isPurchased).length);
  readonly totalPrice = computed(() => this.items().reduce((sum, item) => sum + item.totalPrice, 0));
  readonly groups = computed(() => groupShoppingItems(this.items()));
  readonly form = this.formBuilder.nonNullable.group({
    ingredientId: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(0.001)]],
    unit: ['g', Validators.required],
    note: ['', Validators.maxLength(500)],
  });

  ngOnInit(): void {
    forkJoin({ items: this.shoppingListService.getAll(), ingredients: this.ingredientService.getAll() })
      .pipe(finalize(() => this.isLoading.set(false))).subscribe({
        next: ({ items, ingredients }) => { this.items.set(items); this.ingredients.set(ingredients); },
        error: error => this.error.set(getApiError(error, this.translations.translate('shopping.loadError'))),
      });
  }

  add(): void {
    if (this.form.invalid || this.isSaving()) { this.form.markAllAsTouched(); return; }
    this.isSaving.set(true); this.error.set(null);
    const { ingredientId, quantity, unit, note } = this.form.getRawValue();
    const ingredient = this.selectedIngredient()!;
    const baseQuantity = toBaseQuantity(quantity, unit as DisplayUnit, ingredient);
    const trimmedNote = note.trim();
    // Re-adding something already bought restarts that line instead of stacking a duplicate: the tick,
    // the quantity and the note all reset to what was just entered rather than keeping the old shop's values.
    const purchased = this.items().find(item => item.ingredientId === ingredientId && item.isPurchased);
    const saveItem = purchased
      ? this.shoppingListService.update(purchased, { quantity: baseQuantity, isPurchased: false, note: trimmedNote })
      : this.shoppingListService.create(ingredientId, baseQuantity, trimmedNote || null);
    saveItem.pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: item => {
        this.items.update(items => items.some(current => current.id === item.id)
          ? items.map(current => current.id === item.id ? item : current)
          : [...items, item]);
        this.form.reset({ ingredientId: '', quantity: 1, unit: 'g', note: '' });
      },
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
    if (this.isSaving() || this.purchasedCount() === 0) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.clearChecked().pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => this.items.update(items => items.filter(item => !item.isPurchased)),
      error: error => this.error.set(getApiError(error, this.translations.translate('shopping.clearError'))),
    });
  }

  generate(): void {
    if (this.isSaving() || this.generateTo() < this.generateFrom()) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.generate(this.generateFrom(), this.generateTo()).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: items => this.items.set(items),
      error: error => this.error.set(getApiError(error, this.translations.translate('shopping.generateError'))),
    });
  }

  selectedIngredient(): Ingredient | undefined { return this.ingredients().find(value => value.id === this.form.controls.ingredientId.value); }
  selectIngredient(ingredient: Ingredient): void { this.form.controls.unit.setValue(ingredient.measurementUnit); }
  selectedUnits(): DisplayUnit[] { return this.selectedIngredient() ? unitsForIngredient(this.selectedIngredient()!) : ['g']; }
  display(item: ShoppingListItem) {
    const ingredient = this.ingredients().find(value => value.id === item.ingredientId);
    return ingredient ? displayMeasurement(item.quantity, ingredient) : { quantity: item.quantity, unit: item.measurementUnit as DisplayUnit };
  }
  updateDisplayQuantity(item: ShoppingListItem, quantity: number): void {
    const ingredient = this.ingredients().find(value => value.id === item.ingredientId);
    if (ingredient) this.update(item, { quantity: toBaseQuantity(quantity, this.display(item).unit, ingredient) });
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
