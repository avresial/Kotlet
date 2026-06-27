import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { Ingredient, IngredientRequest, measurementUnits } from '../../ingredient.models';
import { IngredientService } from '../../ingredient.service';

@Component({
  selector: 'app-ingredients-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './ingredients-page.html',
  styleUrl: './ingredients-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IngredientsPage implements OnInit {
  private readonly service = inject(IngredientService);
  private readonly formBuilder = inject(FormBuilder);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly search = signal('');
  readonly filteredIngredients = computed(() => {
    const query = this.search().trim().toLocaleLowerCase();
    return query
      ? this.ingredients().filter((ingredient) => ingredient.name.toLocaleLowerCase().includes(query))
      : this.ingredients();
  });
  readonly units = measurementUnits;
  readonly editingId = signal<string | null>(null);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly deletingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    measurementUnit: ['g', Validators.required],
    caloriesPer100Grams: [0, [Validators.required, Validators.min(0), Validators.max(999999.99)]],
    price: [0, [Validators.required, Validators.min(0), Validators.max(99999999.99)]],
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.service.getAll().pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: (ingredients) => this.ingredients.set(ingredients),
      error: (error) => this.error.set(getApiError(error, 'Unable to load ingredients.')),
    });
  }

  edit(ingredient: Ingredient): void {
    this.editingId.set(ingredient.id);
    this.error.set(null);
    this.form.setValue({
      name: ingredient.name, measurementUnit: ingredient.measurementUnit,
      caloriesPer100Grams: ingredient.caloriesPer100Grams, price: ingredient.price,
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ name: '', measurementUnit: 'g', caloriesPer100Grams: 0, price: 0 });
  }

  save(): void {
    if (this.form.invalid || this.isSaving()) { this.form.markAllAsTouched(); return; }
    this.isSaving.set(true);
    this.error.set(null);
    const request = this.form.getRawValue() as IngredientRequest;
    const id = this.editingId();
    const operation = id ? this.service.update(id, request) : this.service.create(request);
    operation.pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: (ingredient) => {
        this.ingredients.update((items) => id
          ? items.map((item) => item.id === id ? ingredient : item).sort(this.sortByName)
          : [...items, ingredient].sort(this.sortByName));
        this.cancelEdit();
      },
      error: (error) => this.error.set(getApiError(error, 'Unable to save the ingredient.')),
    });
  }

  remove(ingredient: Ingredient): void {
    if (this.deletingId() || !window.confirm(`Delete ${ingredient.name}?`)) return;
    this.deletingId.set(ingredient.id);
    this.error.set(null);
    this.service.delete(ingredient.id).pipe(finalize(() => this.deletingId.set(null))).subscribe({
      next: () => {
        this.ingredients.update((items) => items.filter((item) => item.id !== ingredient.id));
        if (this.editingId() === ingredient.id) this.cancelEdit();
      },
      error: (error) => this.error.set(getApiError(error, 'Unable to delete the ingredient.')),
    });
  }

  unitLabel(value: string): string { return this.units.find((unit) => unit.value === value)?.label ?? value; }
  private readonly sortByName = (left: Ingredient, right: Ingredient) => left.name.localeCompare(right.name);
}
