import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { CreateRecipeRequest } from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';
import { RecipeForm } from '../../components/recipe-form/recipe-form';

@Component({
  selector: 'app-recipe-create-page',
  imports: [RouterLink, RecipeForm, TranslatePipe],
  templateUrl: './recipe-create-page.html',
  styleUrl: './recipe-create-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeCreatePage {
  private readonly service = inject(RecipeService);
  private readonly router = inject(Router);
  private readonly translations = inject(TranslationService);

  readonly isSaving = signal(false);
  readonly error = signal<string | null>(null);
  private selectedImage: File | null = null;

  save(request: CreateRecipeRequest): void {
    this.isSaving.set(true);
    this.error.set(null);
    this.service.create(request).subscribe({
      next: (recipe) => {
        if (!this.selectedImage) {
          this.router.navigate(['/recipes', recipe.id], { state: { justCreated: true } });
          return;
        }

        this.service.uploadImage(recipe.id, this.selectedImage).subscribe({
          next: () => this.router.navigate(['/recipes', recipe.id], { state: { justCreated: true } }),
          error: () => this.router.navigate(['/recipes', recipe.id, 'edit']),
        });
      },
      error: (err) => {
        this.error.set(getApiError(err, this.translations.translate('recipes.createError')));
        this.isSaving.set(false);
      },
    });
  }

  selectImage(file: File | null): void {
    this.selectedImage = file;
  }

  cancel(): void {
    this.router.navigate(['/recipes']);
  }
}
