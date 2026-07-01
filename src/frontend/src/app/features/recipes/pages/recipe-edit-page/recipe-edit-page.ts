import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { RecipeDetail, UpdateRecipeRequest } from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';
import { RecipeForm } from '../../components/recipe-form/recipe-form';
import { ImageGalleryEditor } from '../../components/image-gallery-editor/image-gallery-editor';

@Component({
  selector: 'app-recipe-edit-page',
  imports: [RouterLink, RecipeForm, ImageGalleryEditor, TranslatePipe],
  templateUrl: './recipe-edit-page.html',
  styleUrl: './recipe-edit-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeEditPage implements OnInit {
  private readonly service = inject(RecipeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly translations = inject(TranslationService);

  readonly recipe = signal<RecipeDetail | null>(null);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly error = signal<string | null>(null);

  get id(): string { return this.route.snapshot.paramMap.get('id')!; }

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => this.load(params.get('id')!));
  }

  private load(id: string): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.service.get(id)
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (recipe) => this.recipe.set(recipe),
        error: (err) => this.error.set(getApiError(err, this.translations.translate('recipes.loadOneError'))),
      });
  }

  save(request: UpdateRecipeRequest): void {
    this.isSaving.set(true);
    this.error.set(null);
    this.service.update(this.id, request).subscribe({
      next: (recipe) => this.router.navigate(['/recipes', recipe.id]),
      error: (err) => {
        this.error.set(getApiError(err, this.translations.translate('recipes.updateError')));
        this.isSaving.set(false);
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/recipes', this.id]);
  }
}
