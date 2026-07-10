import { describe, expect, it } from 'vitest';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AuthService } from '../../../../core/auth/auth.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { RecipeDetail } from '../../models/recipe.models';
import { aggregateIngredientProperties, RecipeDetailPage } from './recipe-detail-page';

const ingredient = (id: string, allergens: number, attributes: number, suitability: number) =>
  ({ id, allergens, attributes, suitability }) as Ingredient;

describe('aggregateIngredientProperties', () => {
  it('deduplicates recipe ingredients, unions warnings, and intersects suitable diets', () => {
    const result = aggregateIngredientProperties(
      ['flour', 'milk', 'flour'],
      [ingredient('flour', 1, 2, 11), ingredient('milk', 64, 4, 2), ingredient('unused', 8, 8, 8)],
    );

    expect(result).toEqual({ allergens: 65, attributes: 6, suitability: 2 });
  });
});

describe('RecipeDetailPage source link', () => {
  function makeRecipe(sourceUrl: string | null): RecipeDetail {
    return {
      id: 'recipe-1',
      title: 'Tomato Soup',
      slug: 'tomato-soup',
      createdByUserId: 'user-1',
      descriptionMarkdown: null,
      servings: 2,
      mealType: null,
      ingredients: [],
      images: [],
      canEdit: false,
      isAiAssisted: false,
      sourceUrl,
      createdAtUtc: '2026-07-10T00:00:00Z',
      updatedAtUtc: '2026-07-10T00:00:00Z',
    };
  }

  function render(sourceUrl: string | null) {
    TestBed.configureTestingModule({
      imports: [RecipeDetailPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'recipe-1' }) } } },
        { provide: AuthService, useValue: { isAuthenticated: () => false } },
      ],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(RecipeDetailPage);
    fixture.detectChanges();
    http.expectOne('/api/recipes/recipe-1').flush(makeRecipe(sourceUrl));
    fixture.detectChanges();
    http.verify();
    return fixture;
  }

  it('shows a source link opening in a new tab with safe attributes', () => {
    const fixture = render('https://example.com/recipe');
    const link = fixture.nativeElement.querySelector('a.source-link') as HTMLAnchorElement;
    expect(link).not.toBeNull();
    expect(link.getAttribute('href')).toBe('https://example.com/recipe');
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('hides the source link when the recipe has no source url', () => {
    const fixture = render(null);
    expect(fixture.nativeElement.querySelector('a.source-link')).toBeNull();
  });
});
