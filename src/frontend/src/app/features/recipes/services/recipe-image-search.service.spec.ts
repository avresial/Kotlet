import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RecipeImageSearchService, buildRecipeImageSearchQuery } from './recipe-image-search.service';

describe('RecipeImageSearchService', () => {
  let service: RecipeImageSearchService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(RecipeImageSearchService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('calls the provider-backed search endpoint with the requested filters', () => {
    service.search('tomato soup', 8).subscribe();

    const request = http.expectOne(request => request.url === '/api/recipes/images/search');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('query')).toBe('tomato soup');
    expect(request.request.params.get('limit')).toBe('8');
    expect(request.request.params.get('orientation')).toBe('landscape');
    request.flush([]);
  });

  it('builds a title-first query and excludes generic ingredients', () => {
    expect(buildRecipeImageSearchQuery('Tomato soup', ['salt', 'basil', 'water', 'garlic']))
      .toBe('Tomato soup basil garlic');
  });

  it('caps appended ingredients', () => {
    expect(buildRecipeImageSearchQuery('Soup', ['one', 'two', 'three', 'four']))
      .toBe('Soup one two three');
  });
});
