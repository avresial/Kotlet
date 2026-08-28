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

const recipeIngredient = {
  id: 'line-1',
  sortOrder: 0,
  ingredientId: 'tomato',
  name: 'Tomato',
  quantity: 500,
  unit: 'g',
  normalizedQuantity: 500,
  normalizedUnit: 'g',
  note: null,
};

function makeRecipe(overrides: Partial<RecipeDetail> = {}): RecipeDetail {
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
    sourceUrl: null,
    videoUrl: null,
    videoThumbnailUrl: null,
    createdAtUtc: '2026-07-10T00:00:00Z',
    updatedAtUtc: '2026-07-10T00:00:00Z',
    ...overrides,
  };
}

describe('RecipeDetailPage source link', () => {
  function render(overrides: Partial<RecipeDetail>) {
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
    http.expectOne('/api/recipes/recipe-1').flush(makeRecipe(overrides));
    fixture.detectChanges();
    http.verify();
    return fixture;
  }

  it('shows a source link opening in a new tab with safe attributes', () => {
    const fixture = render({ sourceUrl: 'https://example.com/recipe' });
    const link = fixture.nativeElement.querySelector('a.source-link') as HTMLAnchorElement;
    expect(link).not.toBeNull();
    expect(link.getAttribute('href')).toBe('https://example.com/recipe');
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('hides the source link when the recipe has no source url', () => {
    const fixture = render({ sourceUrl: null });
    expect(fixture.nativeElement.querySelector('a.source-link')).toBeNull();
  });

  it('renders a playable video with its thumbnail instead of the image hero', () => {
    const fixture = render({
      videoUrl: 'https://media.example.com/soup.mp4',
      videoThumbnailUrl: 'https://media.example.com/soup.jpg',
    });

    const video = fixture.nativeElement.querySelector('app-recipe-video video') as HTMLVideoElement;
    expect(video).not.toBeNull();
    expect(video.getAttribute('src')).toBe('https://media.example.com/soup.mp4');
    expect(video.getAttribute('poster')).toBe('https://media.example.com/soup.jpg');
    expect(fixture.nativeElement.querySelector('app-image-gallery')).toBeNull();
  });
});

describe('RecipeDetailPage shopping-list action placement', () => {
  function render(isAuthenticated: boolean) {
    TestBed.configureTestingModule({
      imports: [RecipeDetailPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'recipe-1' }) } } },
        { provide: AuthService, useValue: { isAuthenticated: () => isAuthenticated, currentUser: () => ({ id: 'user-1', displayName: 'Alex' }) } },
      ],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(RecipeDetailPage);
    fixture.detectChanges();
    http.expectOne('/api/recipes/recipe-1').flush(makeRecipe({ ingredients: [recipeIngredient] }));
    if (isAuthenticated) http.expectOne('/api/ingredients').flush([]);
    fixture.detectChanges();
    if (isAuthenticated) http.expectOne('/api/meal-planner/members').flush([]);
    fixture.detectChanges();
    http.verify();
    return fixture;
  }

  it('renders the action inside the ingredients section heading', () => {
    const fixture = render(true);
    const action = fixture.nativeElement.querySelector('app-recipe-shopping-list');
    expect(action).not.toBeNull();
    expect(action.closest('.ingredients-section .section-heading')).not.toBeNull();
  });

  it('keeps the area between the totals and the ingredient properties free of the action', () => {
    const fixture = render(true);
    const contentCard = fixture.nativeElement.querySelector('.content-card') as HTMLElement;
    const standalone = Array.from(contentCard.children).some((child) => child.matches('app-recipe-shopping-list'));
    expect(standalone).toBe(false);

    const action = contentCard.querySelector('app-recipe-shopping-list') as HTMLElement;
    expect(action.closest('section')?.classList.contains('ingredients-section')).toBe(true);
  });

  it('omits the action for anonymous visitors', () => {
    const fixture = render(false);
    expect(fixture.nativeElement.querySelector('.ingredients-section')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-recipe-shopping-list')).toBeNull();
  });
});
