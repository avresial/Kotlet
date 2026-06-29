import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { finalize } from 'rxjs';
import DOMPurify from 'dompurify';
import { marked } from 'marked';
import { getApiError } from '../../../../core/http/api-error';
import { RecipeDetail, RecipeIngredient } from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';
import { ImageGallery } from '../../components/image-gallery/image-gallery';

@Component({
  selector: 'app-recipe-detail-page',
  imports: [RouterLink, DatePipe, ImageGallery],
  templateUrl: './recipe-detail-page.html',
  styleUrl: './recipe-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeDetailPage implements OnInit {
  private readonly service = inject(RecipeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly sanitizer = inject(DomSanitizer);

  readonly recipe = signal<RecipeDetail | null>(null);
  readonly isLoading = signal(true);
  readonly isDeleting = signal(false);
  readonly error = signal<string | null>(null);
  readonly justCreated = signal(!!this.router.getCurrentNavigation()?.extras.state?.['justCreated']);

  readonly descriptionHtml = computed<SafeHtml>(() => {
    const md = this.recipe()?.descriptionMarkdown;
    if (!md?.trim()) return '';
    const raw = marked.parse(md, { async: false }) as string;
    return this.sanitizer.bypassSecurityTrustHtml(DOMPurify.sanitize(raw));
  });

  get id(): string { return this.route.snapshot.paramMap.get('id')!; }

  displayQuantity(ingredient: RecipeIngredient): number {
    return Number.isInteger(ingredient.quantity) ? ingredient.quantity : ingredient.normalizedQuantity;
  }

  displayUnit(ingredient: RecipeIngredient): string {
    return Number.isInteger(ingredient.quantity) ? ingredient.unit : ingredient.normalizedUnit;
  }

  hasConvertedMeasurement(ingredient: RecipeIngredient): boolean {
    return this.displayQuantity(ingredient) !== ingredient.normalizedQuantity
      || this.displayUnit(ingredient) !== ingredient.normalizedUnit;
  }

  ngOnInit(): void {
    this.service.get(this.id)
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (recipe) => this.recipe.set(recipe),
        error: (err) => this.error.set(getApiError(err, 'Unable to load recipe.')),
      });
  }

  addNext(): void {
    this.router.navigate(['/recipes/new']);
  }

  delete(): void {
    if (!window.confirm(`Delete "${this.recipe()?.title}"?`)) return;
    this.isDeleting.set(true);
    this.service.delete(this.id).subscribe({
      next: () => this.router.navigate(['/recipes']),
      error: (err) => {
        this.error.set(getApiError(err, 'Unable to delete recipe.'));
        this.isDeleting.set(false);
      },
    });
  }
}
