import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  forwardRef,
  input,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  ControlValueAccessor,
  FormBuilder,
  FormGroup,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  ReactiveFormsModule,
  ValidationErrors,
  Validator,
  Validators,
} from '@angular/forms';
import { inject } from '@angular/core';
import { Subscription } from 'rxjs';
import { RecipeIngredientRequest } from '../../models/recipe.models';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { IngredientPicker } from '../../../ingredients/components/ingredient-picker/ingredient-picker';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { DisplayUnit, toBaseQuantity, unitsForIngredient } from '../../../ingredients/display-units';

@Component({
  selector: 'app-ingredient-list-editor',
  imports: [ReactiveFormsModule, IngredientPicker, TranslatePipe],
  templateUrl: './ingredient-list-editor.html',
  styleUrl: './ingredient-list-editor.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => IngredientListEditor),
      multi: true,
    },
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => IngredientListEditor),
      multi: true,
    },
  ],
})
export class IngredientListEditor implements ControlValueAccessor, Validator {
  private readonly fb = inject(FormBuilder);
  private readonly ingredientService = inject(IngredientService);
  private readonly destroyRef = inject(DestroyRef);
  readonly ariaLabelledby = input<string | null>(null);
  readonly isDisabled = signal(false);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly isLoadingIngredients = signal(true);
  readonly ingredientLoadError = signal(false);

  readonly formArray = this.fb.array<FormGroup>([]);

  private onChange: (value: RecipeIngredientRequest[]) => void = () => {};
  private onTouched: () => void = () => {};
  private onValidatorChange: () => void = () => {};
  private changeSubscription?: Subscription;

  constructor() {
    this.ingredientService.getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (ingredients) => {
          this.ingredients.set([...ingredients].sort((a, b) => a.name.localeCompare(b.name)));
          this.isLoadingIngredients.set(false);
          this.rows.forEach((row) => row.get('name')?.updateValueAndValidity({ emitEvent: false }));
          this.formArray.updateValueAndValidity();
        },
        error: () => {
          this.ingredientLoadError.set(true);
          this.isLoadingIngredients.set(false);
        },
      });
  }

  writeValue(value: RecipeIngredientRequest[] | null): void {
    this.formArray.clear({ emitEvent: false });
    (value ?? []).forEach((ing) => this.formArray.push(this.createRow(ing), { emitEvent: false }));
    this.ensureTrailingEmptyRow();
  }

  registerOnChange(fn: (value: RecipeIngredientRequest[]) => void): void {
    this.onChange = fn;
    this.changeSubscription?.unsubscribe();
    this.changeSubscription = this.formArray.valueChanges.subscribe(() => {
      this.ensureTrailingEmptyRow();
      this.emitChange();
      this.onValidatorChange();
    });
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.isDisabled.set(isDisabled);
    isDisabled ? this.formArray.disable() : this.formArray.enable();
  }

  validate(_: AbstractControl): ValidationErrors | null {
    return this.formArray.valid ? null : { ingredients: true };
  }

  registerOnValidatorChange(fn: () => void): void { this.onValidatorChange = fn; }

  get rows() { return this.formArray.controls as FormGroup[]; }

  selectIngredient(row: FormGroup): void {
    const ingredient = this.ingredients().find((item) => item.name === row.get('name')?.value);
    row.get('ingredientId')?.setValue(ingredient?.id ?? '');
    const defaultUnit = ingredient?.isCountable && ingredient.measurementUnitsPerPiece
      ? 'piece'
      : ingredient?.measurementUnit ?? null;
    row.get('unit')?.setValue(defaultUnit);
    this.onTouched();
  }

  removeRow(index: number): void {
    this.formArray.removeAt(index);
    this.ensureTrailingEmptyRow();
    this.onTouched();
  }

  moveUp(index: number): void {
    if (index === 0) return;
    const item = this.formArray.at(index);
    this.formArray.removeAt(index, { emitEvent: false });
    this.formArray.insert(index - 1, item);
  }

  moveDown(index: number): void {
    if (index >= this.formArray.length - 1) return;
    const item = this.formArray.at(index);
    this.formArray.removeAt(index, { emitEvent: false });
    this.formArray.insert(index + 1, item);
  }

  unitsFor(row: FormGroup): { value: string; label: string }[] {
    const ingredient = this.ingredients().find((item) => item.id === row.get('ingredientId')?.value);
    if (!ingredient) return [];
    const labels: Record<DisplayUnit, string> = { g: 'units.grams', kg: 'units.kilograms', ml: 'units.millilitres', l: 'units.litres', piece: 'units.piece' };
    const units = [
      ...unitsForIngredient(ingredient).map(value => ({ value, label: labels[value] })),
      { value: 'tsp', label: 'units.teaspoon' },
      { value: 'tbsp', label: 'units.tablespoon' },
      { value: 'cup', label: 'units.cup' },
    ];
    return units;
  }

  private createRow(ing?: RecipeIngredientRequest): FormGroup {
    const row = this.fb.group({
      ingredientId: [ing?.ingredientId ?? ''],
      name: [ing?.name ?? ''],
      quantity: [ing?.quantity ?? null],
      unit: [ing?.unit ?? ''],
      note: [ing?.note ?? null, Validators.maxLength(300)],
    });
    row.get('quantity')?.setValidators([
      (control: AbstractControl) => this.rowHasData(row) && control.value == null ? { required: true } : null,
      Validators.min(0.001),
    ]);
    row.get('unit')?.setValidators([
      (control: AbstractControl) => this.rowHasData(row) && !control.value ? { required: true } : null,
      Validators.maxLength(40),
    ]);
    row.get('name')?.setValidators([
        (control: AbstractControl) => this.rowHasData(row) && !control.value ? { required: true } : null,
        Validators.maxLength(200),
        (control: AbstractControl) => this.isLoadingIngredients()
          || !control.value
          || this.ingredients().some((item) =>
            item.id === row.get('ingredientId')?.value || item.name === control.value)
          ? null
          : { unknownIngredient: true },
      ]);
    return row;
  }

  private emitChange(): void {
    this.onChange(this.formArray.getRawValue().filter(row => this.rowHasData(row)).map(row => {
      const ingredient = this.ingredients().find(item => item.id === row['ingredientId']);
      return ingredient && ['kg', 'l'].includes(row['unit'])
        ? { ...row, quantity: toBaseQuantity(row['quantity'], row['unit'] as DisplayUnit, ingredient), unit: ingredient.measurementUnit }
        : row;
    }) as RecipeIngredientRequest[]);
  }

  private ensureTrailingEmptyRow(): void {
    if (this.rows.length === 0 || this.rowHasData(this.rows[this.rows.length - 1])) {
      this.formArray.push(this.createRow(), { emitEvent: false });
    }
    this.rows.forEach(row => {
      row.get('name')?.updateValueAndValidity({ emitEvent: false });
      row.get('quantity')?.updateValueAndValidity({ emitEvent: false });
      row.get('unit')?.updateValueAndValidity({ emitEvent: false });
    });
  }

  private rowHasData(row: FormGroup | Record<string, unknown>): boolean {
    const value = row instanceof FormGroup ? row.getRawValue() : row;
    return value['name'] !== '' || value['quantity'] != null || value['note'] != null && value['note'] !== '';
  }
}
