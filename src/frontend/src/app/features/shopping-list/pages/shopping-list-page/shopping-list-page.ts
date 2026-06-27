import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { ShoppingListItem } from '../../shopping-list.models';
import { ShoppingListService } from '../../shopping-list.service';

@Component({
  selector: 'app-shopping-list-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './shopping-list-page.html',
  styleUrl: './shopping-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShoppingListPage implements OnInit {
  private readonly shoppingListService = inject(ShoppingListService);
  private readonly ingredientService = inject(IngredientService);
  private readonly formBuilder = inject(FormBuilder);
  readonly items = signal<ShoppingListItem[]>([]);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly error = signal<string | null>(null);
  readonly availableIngredients = computed(() => this.ingredients().filter(ingredient =>
    !this.items().some(item => item.ingredientId === ingredient.id)));
  readonly purchasedCount = computed(() => this.items().filter(item => item.isPurchased).length);
  readonly totalPrice = computed(() => this.items().reduce((sum, item) => sum + item.totalPrice, 0));
  readonly form = this.formBuilder.nonNullable.group({
    ingredientId: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(0.001)]],
  });

  ngOnInit(): void {
    forkJoin({ items: this.shoppingListService.getAll(), ingredients: this.ingredientService.getAll() })
      .pipe(finalize(() => this.isLoading.set(false))).subscribe({
        next: ({ items, ingredients }) => { this.items.set(items); this.ingredients.set(ingredients); },
        error: error => this.error.set(getApiError(error, 'Unable to load your shopping list.')),
      });
  }

  add(): void {
    if (this.form.invalid || this.isSaving()) { this.form.markAllAsTouched(); return; }
    this.isSaving.set(true); this.error.set(null);
    const { ingredientId, quantity } = this.form.getRawValue();
    this.shoppingListService.create(ingredientId, quantity).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: item => { this.items.update(items => [...items, item]); this.form.reset({ ingredientId: '', quantity: 1 }); },
      error: error => this.error.set(getApiError(error, 'Unable to add this ingredient.')),
    });
  }

  update(item: ShoppingListItem, changes: Partial<Pick<ShoppingListItem, 'quantity' | 'isPurchased'>>): void {
    if (changes.quantity !== undefined && (!Number.isFinite(changes.quantity) || changes.quantity <= 0)) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.update(item, changes).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: updated => this.items.update(items => items.map(current => current.id === updated.id ? updated : current)),
      error: error => this.error.set(getApiError(error, 'Unable to update this item.')),
    });
  }

  remove(item: ShoppingListItem): void {
    if (this.isSaving()) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.delete(item.id).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => this.items.update(items => items.filter(current => current.id !== item.id)),
      error: error => this.error.set(getApiError(error, 'Unable to remove this item.')),
    });
  }

  clearChecked(): void {
    if (this.isSaving() || this.purchasedCount() === 0) return;
    this.isSaving.set(true); this.error.set(null);
    this.shoppingListService.clearChecked().pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => this.items.update(items => items.filter(item => !item.isPurchased)),
      error: error => this.error.set(getApiError(error, 'Unable to clear checked items.')),
    });
  }
}
