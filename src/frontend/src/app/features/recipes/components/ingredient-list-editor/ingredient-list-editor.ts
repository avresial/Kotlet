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
  FormArray,
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
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-ingredient-list-editor',
  imports: [ReactiveFormsModule],
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
  }

  registerOnChange(fn: (value: RecipeIngredientRequest[]) => void): void {
    this.onChange = fn;
    this.changeSubscription?.unsubscribe();
    this.changeSubscription = this.formArray.valueChanges.subscribe(() => this.emitChange());
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

  get rows() { return this.formArray.controls as FormGroup[]; }

  addRow(): void {
    this.formArray.push(this.createRow());
    this.onTouched();
  }

  selectIngredient(row: FormGroup): void {
    const ingredient = this.ingredients().find((item) => item.name === row.get('name')?.value);
    row.get('unit')?.setValue(ingredient?.measurementUnit ?? null);
    this.onTouched();
  }

  removeRow(index: number): void {
    this.formArray.removeAt(index);
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

  private createRow(ing?: RecipeIngredientRequest): FormGroup {
    return this.fb.group({
      name: [ing?.name ?? '', [
        Validators.required,
        Validators.maxLength(200),
        (control: AbstractControl) => this.isLoadingIngredients()
          || this.ingredients().some((item) => item.name === control.value)
          ? null
          : { unknownIngredient: true },
      ]],
      quantity: [ing?.quantity ?? null],
      unit: [ing?.unit ?? null, Validators.maxLength(40)],
      note: [ing?.note ?? null, Validators.maxLength(300)],
    });
  }

  private emitChange(): void {
    this.onChange(this.formArray.value as RecipeIngredientRequest[]);
  }
}
