import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Ingredient } from '../../../ingredients/ingredient.models';
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
    fixture.detectChanges();
    const chip = fixture.nativeElement.querySelector('.filter-chip') as HTMLElement;
    expect(chip.textContent).toContain('Tomato');
    expect(fixture.componentInstance.availableIngredients()).toEqual([]);

    (chip.querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.filter-chip')).toBeNull();
    expect(fixture.componentInstance.selectedIngredientIds()).toEqual([]);
    http.verify();
  });
});
