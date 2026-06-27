import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { RecipeDetail, UpdateRecipeRequest } from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';
import { RecipeForm } from '../../components/recipe-form/recipe-form';

@Component({
  selector: 'app-recipe-edit-page',
  imports: [RouterLink, RecipeForm],
  templateUrl: './recipe-edit-page.html',
  styleUrl: './recipe-edit-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeEditPage implements OnInit {
  private readonly service = inject(RecipeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly recipe = signal<RecipeDetail | null>(null);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly error = signal<string | null>(null);

  get id(): string { return this.route.snapshot.paramMap.get('id')!; }

  ngOnInit(): void {
    this.service.get(this.id)
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (recipe) => this.recipe.set(recipe),
        error: (err) => this.error.set(getApiError(err, 'Unable to load recipe.')),
      });
  }

  save(request: UpdateRecipeRequest): void {
    this.isSaving.set(true);
    this.error.set(null);
    this.service.update(this.id, request).subscribe({
      next: (recipe) => this.router.navigate(['/recipes', recipe.id]),
      error: (err) => {
        this.error.set(getApiError(err, 'Unable to update recipe.'));
        this.isSaving.set(false);
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/recipes', this.id]);
  }
}
