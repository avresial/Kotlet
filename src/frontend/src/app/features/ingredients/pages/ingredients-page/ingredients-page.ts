import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal, WritableSignal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { allergenOptions, dietarySuitabilityOptions, foodAttributeOptions, foodCategories, Ingredient, IngredientRequest, measurementUnits } from '../../ingredient.models';
import { IngredientService } from '../../ingredient.service';
import { DisplayUnit, displayUnitOptions, fromBasePer100, toBasePer100 } from '../../display-units';

const DEFAULT_LANGUAGE = 'en';

export function filterIngredients(ingredients: Ingredient[], search: string, category: number | null): Ingredient[] {
  const query = search.trim().toLocaleLowerCase();
  return ingredients.filter(ingredient =>
    (category === null || ingredient.category === category)
    && (!query || ingredient.name.toLocaleLowerCase().includes(query)));
}

export function paginate<T>(items: T[], page: number, pageSize: number): T[] {
  return items.slice((page - 1) * pageSize, page * pageSize);
}

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
  private readonly route = inject(ActivatedRoute);
  readonly language = this.translation.language;
  /** The translation field is only relevant when editing in a non-default language. */
  readonly showTranslation = computed(() => this.language() !== DEFAULT_LANGUAGE);
  readonly languageBadge = computed(() => this.language().toUpperCase());
  readonly ingredients = signal<Ingredient[]>([]);
  readonly search = signal('');
  readonly selectedCategory = signal<number | null>(null);
  readonly hasFilters = computed(() => !!this.search().trim() || this.selectedCategory() !== null);
  readonly filteredIngredients = computed(() => filterIngredients(this.ingredients(), this.search(), this.selectedCategory()));
  readonly pageSize = 20;
  private readonly requestedPage = signal(1);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filteredIngredients().length / this.pageSize)));
  /** The requested page clamped to the available range, so filtering or deleting never strands the user on an empty page. */
  readonly page = computed(() => Math.min(this.requestedPage(), this.totalPages()));
  readonly pagedIngredients = computed(() => paginate(this.filteredIngredients(), this.page(), this.pageSize));
  readonly units = measurementUnits;
  readonly displayUnits = displayUnitOptions;
  readonly categories = foodCategories;
  readonly allergenOptions = allergenOptions;
  readonly attributeOptions = foodAttributeOptions;
  readonly suitabilityOptions = dietarySuitabilityOptions;
  readonly allergens = signal(0);
  readonly attributes = signal(0);
  readonly suitability = signal(0);
  readonly editingId = signal<string | null>(null);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly deletingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    translation: ['', [Validators.maxLength(150)]],
    measurementUnit: ['g', Validators.required],
    pieceBaseUnit: ['g', Validators.required],
    category: [0, Validators.required],
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
      next: (ingredients) => {
        this.ingredients.set(ingredients);
        const editId = this.route.snapshot.queryParamMap.get('edit');
        const ingredient = ingredients.find(item => item.id === editId);
        if (ingredient) this.edit(ingredient);
      },
      error: (error) => this.error.set(getApiError(error, this.translation.translate('ingredients.loadError'))),
    });
  }

  setSearch(value: string): void {
    this.search.set(value);
    this.requestedPage.set(1);
  }

  setCategory(value: number | null): void {
    this.selectedCategory.set(value);
    this.requestedPage.set(1);
  }

  clearFilters(): void {
    this.search.set('');
    this.selectedCategory.set(null);
    this.requestedPage.set(1);
  }

  prevPage(): void {
    this.requestedPage.set(Math.max(1, this.page() - 1));
  }

  nextPage(): void {
    this.requestedPage.set(Math.min(this.totalPages(), this.page() + 1));
  }

  toggleFlag(mask: WritableSignal<number>, bit: number): void {
    mask.update((value) => value ^ bit);
  }

  hasFlag(mask: number, bit: number): boolean {
    return (mask & bit) !== 0;
  }

  edit(ingredient: Ingredient): void {
    this.editingId.set(ingredient.id);
    this.error.set(null);
    this.allergens.set(ingredient.allergens);
    this.attributes.set(ingredient.attributes);
    this.suitability.set(ingredient.suitability);
    const displayUnit: DisplayUnit = ingredient.isCountable ? 'piece' : ingredient.measurementUnit as 'g' | 'ml';
    this.form.setValue({
      name: ingredient.defaultName, translation: ingredient.translation ?? '',
      measurementUnit: displayUnit, pieceBaseUnit: ingredient.measurementUnit, category: ingredient.category,
      measurementUnitsPerPiece: ingredient.measurementUnitsPerPiece,
      caloriesPer100BaseUnits: fromBasePer100(ingredient.caloriesPer100BaseUnits, displayUnit, ingredient.measurementUnitsPerPiece),
      pricePer100BaseUnits: fromBasePer100(ingredient.pricePer100BaseUnits, displayUnit, ingredient.measurementUnitsPerPiece),
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.allergens.set(0);
    this.attributes.set(0);
    this.suitability.set(0);
    this.form.reset({ name: '', translation: '', measurementUnit: 'g', pieceBaseUnit: 'g', category: 0, measurementUnitsPerPiece: null,
      caloriesPer100BaseUnits: 0, pricePer100BaseUnits: 0 });
  }

  save(): void {
    if (this.form.invalid || this.isSaving()) { this.form.markAllAsTouched(); return; }
    this.isSaving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    const displayUnit = value.measurementUnit as DisplayUnit;
    const isCountable = displayUnit === 'piece';
    if (isCountable && !value.measurementUnitsPerPiece) {
      this.form.controls.measurementUnitsPerPiece.setErrors({ required: true });
      this.form.controls.measurementUnitsPerPiece.markAsTouched();
      return;
    }
    const baseUnit = displayUnit === 'kg' ? 'g' : displayUnit === 'l' ? 'ml'
      : displayUnit === 'piece' ? value.pieceBaseUnit : displayUnit;
    const request: IngredientRequest = {
      name: value.name,
      measurementUnit: baseUnit,
      category: value.category,
      isCountable,
      measurementUnitsPerPiece: isCountable ? value.measurementUnitsPerPiece : null,
      caloriesPer100BaseUnits: toBasePer100(value.caloriesPer100BaseUnits, displayUnit, value.measurementUnitsPerPiece),
      pricePer100BaseUnits: toBasePer100(value.pricePer100BaseUnits, displayUnit, value.measurementUnitsPerPiece),
      allergens: this.allergens(),
      attributes: this.attributes(),
      suitability: this.suitability(),
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
  allergenNames(ingredient: Ingredient): string {
    const names = this.allergenOptions
      .filter((option) => (ingredient.allergens & option.value) !== 0)
      .map((option) => this.translation.translate(option.label));
    return names.length > 0 ? names.join(', ') : '—';
  }
  nutritionBasis(): string {
    const unit = this.form.controls.measurementUnit.value;
    return unit === 'g' || unit === 'ml' ? `100 ${unit}` : `1 ${this.translation.translate(this.displayUnits.find(option => option.value === unit)?.label ?? unit)}`;
  }
  private readonly sortByName = (left: Ingredient, right: Ingredient) => left.name.localeCompare(right.name);
}
