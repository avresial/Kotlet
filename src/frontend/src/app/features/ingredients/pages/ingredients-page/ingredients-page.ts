import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { foodCategories, Ingredient, IngredientRequest, measurementUnits } from '../../ingredient.models';
import { IngredientService } from '../../ingredient.service';

const DEFAULT_LANGUAGE = 'en';

@Component({
  selector: 'app-ingredients-page',
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './ingredients-page.html',
  styleUrl: './ingredients-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IngredientsPage implements OnInit {
  private readonly service = inject(IngredientService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly translation = inject(TranslationService);
  readonly language = this.translation.language;
  /** The translation field is only relevant when editing in a non-default language. */
  readonly showTranslation = computed(() => this.language() !== DEFAULT_LANGUAGE);
  readonly languageBadge = computed(() => this.language().toUpperCase());
  readonly ingredients = signal<Ingredient[]>([]);
  readonly search = signal('');
  readonly filteredIngredients = computed(() => {
    const query = this.search().trim().toLocaleLowerCase();
    return query
      ? this.ingredients().filter((ingredient) => ingredient.name.toLocaleLowerCase().includes(query))
      : this.ingredients();
  });
  readonly units = measurementUnits;
  readonly categories = foodCategories;
  readonly editingId = signal<string | null>(null);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly deletingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    translation: ['', [Validators.maxLength(150)]],
    measurementUnit: ['g', Validators.required],
    category: [0, Validators.required],
    isCountable: [false],
    measurementUnitsPerPiece: [null as number | null, [Validators.min(0.001), Validators.max(999999999.999)]],
    caloriesPer100BaseUnits: [0, [Validators.required, Validators.min(0), Validators.max(999999.99)]],
    pricePer100BaseUnits: [0, [Validators.required, Validators.min(0), Validators.max(99999999.99)]],
  });

  ngOnInit(): void {
    if (this.showTranslation())
      this.form.controls.translation.addValidators(Validators.required);
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.service.getAll().pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: (ingredients) => this.ingredients.set(ingredients),
      error: (error) => this.error.set(getApiError(error, this.translation.translate('ingredients.loadError'))),
    });
  }

  edit(ingredient: Ingredient): void {
    this.editingId.set(ingredient.id);
    this.error.set(null);
    this.form.setValue({
      name: ingredient.defaultName, translation: ingredient.translation ?? '',
      measurementUnit: ingredient.measurementUnit, category: ingredient.category,
      isCountable: ingredient.isCountable, measurementUnitsPerPiece: ingredient.measurementUnitsPerPiece,
      caloriesPer100BaseUnits: ingredient.caloriesPer100BaseUnits,
      pricePer100BaseUnits: ingredient.pricePer100BaseUnits,
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ name: '', translation: '', measurementUnit: 'g', category: 0, isCountable: false, measurementUnitsPerPiece: null,
      caloriesPer100BaseUnits: 0, pricePer100BaseUnits: 0 });
  }

  save(): void {
    if (this.form.invalid || this.isSaving()) { this.form.markAllAsTouched(); return; }
    this.isSaving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    if (value.isCountable && !value.measurementUnitsPerPiece) {
      this.form.controls.measurementUnitsPerPiece.setErrors({ required: true });
      this.form.controls.measurementUnitsPerPiece.markAsTouched();
      return;
    }
    const request: IngredientRequest = {
      name: value.name,
      measurementUnit: value.measurementUnit,
      category: value.category,
      isCountable: value.isCountable,
      measurementUnitsPerPiece: value.isCountable ? value.measurementUnitsPerPiece : null,
      caloriesPer100BaseUnits: value.caloriesPer100BaseUnits,
      pricePer100BaseUnits: value.pricePer100BaseUnits,
      ...(this.showTranslation() ? { translation: value.translation.trim() } : {}),
    };
    const id = this.editingId();
    const operation = id ? this.service.update(id, request) : this.service.create(request);
    operation.pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: (ingredient) => {
        this.ingredients.update((items) => id
          ? items.map((item) => item.id === id ? ingredient : item).sort(this.sortByName)
          : [...items, ingredient].sort(this.sortByName));
        this.cancelEdit();
      },
      error: (error) => this.error.set(getApiError(error, this.translation.translate('ingredients.saveError'))),
    });
  }

  remove(ingredient: Ingredient): void {
    if (this.deletingId() || !window.confirm(this.translation.translate('ingredients.deleteConfirm').replace('{name}', ingredient.name))) return;
    this.deletingId.set(ingredient.id);
    this.error.set(null);
    this.service.delete(ingredient.id).pipe(finalize(() => this.deletingId.set(null))).subscribe({
      next: () => {
        this.ingredients.update((items) => items.filter((item) => item.id !== ingredient.id));
        if (this.editingId() === ingredient.id) this.cancelEdit();
      },
      error: (error) => this.error.set(getApiError(error, this.translation.translate('ingredients.deleteError'))),
    });
  }

  unitLabel(value: string): string { return this.translation.translate(this.units.find((unit) => unit.value === value)?.label ?? value); }
  private readonly sortByName = (left: Ingredient, right: Ingredient) => left.name.localeCompare(right.name);
}
