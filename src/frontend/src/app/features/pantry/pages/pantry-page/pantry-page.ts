import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { IngredientPicker } from '../../../ingredients/components/ingredient-picker/ingredient-picker';
import { PantryItem, storageLocations } from '../../pantry.models';
import { PantryService } from '../../pantry.service';

@Component({
  selector: 'app-pantry-page', imports: [ReactiveFormsModule, RouterLink, IngredientPicker, TranslatePipe],
  templateUrl: './pantry-page.html', styleUrl: './pantry-page.scss', changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PantryPage implements OnInit {
  private readonly pantryService = inject(PantryService);
  private readonly ingredientService = inject(IngredientService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly translations = inject(TranslationService);
  readonly items = signal<PantryItem[]>([]);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly availableIngredients = computed(() => this.ingredients().filter(i => !this.items().some(item => item.ingredientId === i.id)));
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly updatingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly locations = storageLocations;
  readonly form = this.formBuilder.group({ ingredientId: ['', Validators.required], quantity: [1, [Validators.required, Validators.min(0)]], expirationDate: [null as string | null], storageLocation: [1, Validators.required] });

  ngOnInit(): void {
    forkJoin({ items: this.pantryService.getAll(), ingredients: this.ingredientService.getAll() })
      .pipe(finalize(() => this.isLoading.set(false))).subscribe({
        next: ({ items, ingredients }) => { this.items.set(items); this.ingredients.set(ingredients); },
        error: error => this.error.set(getApiError(error, this.translations.translate('pantry.loadError'))),
      });
  }

  add(): void {
    if (this.form.invalid || this.isSaving()) { this.form.markAllAsTouched(); return; }
    this.isSaving.set(true); this.error.set(null);
    const { ingredientId, quantity, expirationDate, storageLocation } = this.form.getRawValue();
    this.pantryService.create(ingredientId!, quantity!, expirationDate, storageLocation!).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: item => { this.items.update(items => this.sort([...items, item])); this.form.reset({ ingredientId: '', quantity: 1, expirationDate: null, storageLocation: 1 }); },
      error: error => this.error.set(getApiError(error, this.translations.translate('pantry.addError'))),
    });
  }

  setQuantity(item: PantryItem, quantity: number): void {
    if (!Number.isFinite(quantity)) return;
    const normalized = Math.max(0, Math.round(quantity * 1000) / 1000);
    if (this.updatingId() || normalized === item.quantity) return;
    this.updatingId.set(item.id); this.error.set(null);
    this.pantryService.update(item.id, normalized).pipe(finalize(() => this.updatingId.set(null))).subscribe({
      next: updated => this.items.update(items => this.sort(items.map(current => current.id === updated.id ? updated : current))),
      error: error => this.error.set(getApiError(error, this.translations.translate('pantry.updateError'))),
    });
  }

  remove(item: PantryItem): void {
    if (this.updatingId() || !window.confirm(this.translations.translate('pantry.removeConfirm').replace('{name}', item.ingredientName))) return;
    this.updatingId.set(item.id); this.error.set(null);
    this.pantryService.delete(item.id).pipe(finalize(() => this.updatingId.set(null))).subscribe({
      next: () => this.items.update(items => items.filter(current => current.id !== item.id)),
      error: error => this.error.set(getApiError(error, this.translations.translate('pantry.removeError'))),
    });
  }

  selectedIngredient(): Ingredient | undefined {
    return this.ingredients().find(ingredient => ingredient.id === this.form.controls.ingredientId.value);
  }

  step(item: PantryItem): number { return ['kg', 'l'].includes(item.measurementUnit) ? 0.1 : 1; }
  locationLabel(value: PantryItem['storageLocation']): string {
    return value ? this.translations.translate(this.locations.find(location => location.value === value)!.label) : '';
  }
  private sort(items: PantryItem[]): PantryItem[] { return [...items].sort((a, b) => a.quantity - b.quantity || a.ingredientName.localeCompare(b.ingredientName)); }
}
