import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnInit,
  output,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreateRecipeRequest, RecipeDetail, RecipeIngredientRequest } from '../../models/recipe.models';
import { IngredientListEditor } from '../ingredient-list-editor/ingredient-list-editor';
import { MarkdownEditor } from '../markdown-editor/markdown-editor';

@Component({
  selector: 'app-recipe-form',
  imports: [ReactiveFormsModule, IngredientListEditor, MarkdownEditor],
  templateUrl: './recipe-form.html',
  styleUrl: './recipe-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeForm implements OnInit {
  private readonly fb = inject(FormBuilder);

  readonly initialValue = input<RecipeDetail | null>(null);
  readonly isSaving = input(false);
  readonly error = input<string | null>(null);
  readonly submitLabel = input('Save recipe');
  readonly submitted = output<CreateRecipeRequest>();
  readonly cancelled = output<void>();

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    descriptionMarkdown: [''],
    ingredients: [[] as RecipeIngredientRequest[]],
  });

  ngOnInit(): void {
    const initial = this.initialValue();
    if (initial) {
      this.form.setValue({
        title: initial.title,
        descriptionMarkdown: initial.descriptionMarkdown ?? '',
        ingredients: initial.ingredients.map((i) => ({
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
      title: value.title,
      descriptionMarkdown: value.descriptionMarkdown || null,
      ingredients: value.ingredients,
    });
  }

  cancel(): void {
    this.cancelled.emit();
  }
}
