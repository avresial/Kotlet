import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnInit,
  output,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreateRecipeRequest, RecipeDetail, RecipeIngredientRequest, recipeMealTypes } from '../../models/recipe.models';
import { IngredientListEditor } from '../ingredient-list-editor/ingredient-list-editor';
import { MarkdownEditor } from '../markdown-editor/markdown-editor';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

@Component({
  selector: 'app-recipe-form',
  imports: [ReactiveFormsModule, IngredientListEditor, MarkdownEditor, TranslatePipe],
  templateUrl: './recipe-form.html',
  styleUrl: './recipe-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly translations = inject(TranslationService);

  readonly initialValue = input<RecipeDetail | null>(null);
  readonly isSaving = input(false);
  readonly error = input<string | null>(null);
  readonly submitLabel = input('');
  readonly showImagePicker = input(false);
  readonly submitted = output<CreateRecipeRequest>();
  readonly imageSelected = output<File | null>();
  readonly cancelled = output<void>();

  selectedImage: File | null = null;
  imageError: string | null = null;
  readonly mealTypes = recipeMealTypes;

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(160)]],
    servings: [1, [Validators.required, Validators.min(1), Validators.max(99)]],
    mealType: [null as string | null],
    descriptionMarkdown: [''],
    ingredients: [[] as RecipeIngredientRequest[]],
  });

  ngOnInit(): void {
    const initial = this.initialValue();
    if (initial) {
      this.form.setValue({
        title: initial.title,
        servings: initial.servings,
        mealType: initial.mealType,
        descriptionMarkdown: initial.descriptionMarkdown ?? '',
        ingredients: initial.ingredients.map((i) => ({
          ingredientId: i.ingredientId,
          name: i.name,
          quantity: i.quantity,
          unit: i.unit,
          note: i.note,
        })),
      });
    }
  }

  submit(): void {
    if (this.form.invalid || this.isSaving()) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.submitted.emit({
      title: value.title.trim(),
      servings: value.servings,
      mealType: value.mealType as CreateRecipeRequest['mealType'],
      descriptionMarkdown: value.descriptionMarkdown || null,
      ingredients: value.ingredients,
    });
  }

  cancel(): void {
    this.cancelled.emit();
  }

  chooseImage(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.imageError = null;

    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      this.imageError = this.translations.translate('recipes.imageTypeError');
      input.value = '';
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      this.imageError = this.translations.translate('recipes.imageSizeError');
      input.value = '';
      return;
    }

    this.selectedImage = file;
    this.imageSelected.emit(file);
  }

  removeImage(input: HTMLInputElement): void {
    input.value = '';
    this.selectedImage = null;
    this.imageError = null;
    this.imageSelected.emit(null);
  }
}
