import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { RecipeSummary } from '../../models/recipe.models';
import { RecipeListPage } from './recipe-list-page';

describe('RecipeListPage ingredient filters', () => {
  it('adds selected ingredients as removable chips', () => {
    TestBed.configureTestingModule({
      imports: [RecipeListPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(RecipeListPage);
    const ingredient = { id: 'ingredient-1', name: 'Tomato' } as Ingredient;

    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/recipes').flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    http.expectOne('/api/ingredients').flush([ingredient]);

    fixture.componentInstance.addIngredient(ingredient);
    http.expectOne(request => request.url === '/api/recipes' && request.params.getAll('ingredientIds')?.join() === 'ingredient-1')
      .flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    fixture.detectChanges();
    const chip = fixture.nativeElement.querySelector('.filter-chip') as HTMLElement;
    expect(chip.textContent).toContain('Tomato');
    expect(fixture.componentInstance.availableIngredients()).toEqual([]);

    (chip.querySelector('button') as HTMLButtonElement).click();
    http.expectOne(request => request.url === '/api/recipes' && !request.params.has('ingredientIds'))
      .flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.filter-chip')).toBeNull();
    expect(fixture.componentInstance.selectedIngredientIds()).toEqual([]);
    http.verify();
  });
});

describe('RecipeListPage pagination', () => {
  function makeRecipe(index: number): RecipeSummary {
    return {
      id: `recipe-${index}`,
      title: `Recipe ${index}`,
      slug: `recipe-${index}`,
      createdByUserId: 'user-1',
      ingredientCount: 0,
      servings: 2,
      mealType: null,
      firstImageUrl: null,
      isAiAssisted: false,
      createdAtUtc: '2026-06-29T00:00:00Z',
      updatedAtUtc: '2026-06-29T00:00:00Z',
    };
  }

  function renderWithTotalCount(totalCount: number) {
    TestBed.configureTestingModule({
      imports: [RecipeListPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(RecipeListPage);
    const pageSize = fixture.componentInstance.pageSize;
    const items = Array.from({ length: Math.min(totalCount, pageSize) }, (_, i) => makeRecipe(i + 1));

    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/recipes').flush({ items, page: 1, pageSize, totalCount });
    http.expectOne('/api/ingredients').flush([]);
    fixture.detectChanges();
    http.verify();
    return fixture;
  }

  it('hides the pagination controls when all recipes fit on a single page', () => {
    const fixture = renderWithTotalCount(5);
    expect(fixture.nativeElement.querySelector('.pagination')).toBeNull();
  });

  it('hides the pagination controls when the results exactly fill one page', () => {
    const fixture = renderWithTotalCount(20);
    expect(fixture.nativeElement.querySelector('.pagination')).toBeNull();
  });

  it('shows the pagination controls when recipes span multiple pages', () => {
    const fixture = renderWithTotalCount(21);
    const pagination = fixture.nativeElement.querySelector('.pagination') as HTMLElement;
    expect(pagination).not.toBeNull();
    const [prev, next] = Array.from(pagination.querySelectorAll('button')) as HTMLButtonElement[];
    expect(prev.disabled).toBe(true);
    expect(next.disabled).toBe(false);
  });
});
