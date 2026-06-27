import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { RecipeSummary } from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';

@Component({
  selector: 'app-recipe-list-page',
  imports: [RouterLink, FormsModule, DatePipe],
  templateUrl: './recipe-list-page.html',
  styleUrl: './recipe-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeListPage implements OnInit {
  private readonly service = inject(RecipeService);
  private readonly router = inject(Router);

  readonly recipes = signal<RecipeSummary[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly deletingId = signal<string | null>(null);
  readonly search = signal('');
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 20;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.service
      .list(this.page(), this.pageSize, this.search() || undefined)
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (res) => {
          this.recipes.set(res.items);
          this.totalCount.set(res.totalCount);
        },
        error: (err) => this.error.set(getApiError(err, 'Unable to load recipes.')),
      });
  }

  onSearch(): void {
    this.page.set(1);
    this.load();
  }

  clearSearch(): void {
    this.search.set('');
    this.onSearch();
  }

  prevPage(): void {
    if (this.page() <= 1) return;
    this.page.update((p) => p - 1);
    this.load();
  }

  nextPage(): void {
    if (this.page() * this.pageSize >= this.totalCount()) return;
    this.page.update((p) => p + 1);
    this.load();
  }

  delete(recipe: RecipeSummary): void {
    if (this.deletingId() || !window.confirm(`Delete "${recipe.title}"?`)) return;
    this.deletingId.set(recipe.id);
    this.service.delete(recipe.id).pipe(finalize(() => this.deletingId.set(null))).subscribe({
      next: () => {
        this.recipes.update((items) => items.filter((r) => r.id !== recipe.id));
        this.totalCount.update((n) => n - 1);
      },
      error: (err) => this.error.set(getApiError(err, 'Unable to delete recipe.')),
    });
  }

  navigateTo(recipe: RecipeSummary): void {
    this.router.navigate(['/recipes', recipe.id]);
  }

  get hasNextPage(): boolean {
    return this.page() * this.pageSize < this.totalCount();
  }
}
