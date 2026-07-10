import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RecipeService } from './recipe.service';

describe('RecipeService image gallery', () => {
  let service: RecipeService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(RecipeService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('sends every selected ingredient as a repeated query parameter', () => {
    service.list(2, 20, 'soup', 'dinner', ['ingredient-1', 'ingredient-2']).subscribe();
    const request = http.expectOne(request => request.url === '/api/recipes');
    expect(request.request.params.getAll('ingredientIds')).toEqual(['ingredient-1', 'ingredient-2']);
    expect(request.request.params.get('search')).toBe('soup');
    expect(request.request.params.get('mealType')).toBe('dinner');
    request.flush({ items: [], page: 2, pageSize: 20, totalCount: 0 });
  });

  it('uploads a multipart image with alt text', () => {
    const file = new File(['image'], 'soup.webp', { type: 'image/webp' });
    service.uploadImage('recipe-1', file, 'Soup').subscribe();
    const request = http.expectOne('/api/recipes/recipe-1/images');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.get('file')).toBe(file);
    expect(request.request.body.get('altText')).toBe('Soup');
    request.flush({});
  });

  it('uploads generated image attribution with the multipart image', () => {
    const file = new File(['image'], 'generated.webp', { type: 'image/webp' });
    service.uploadImage('recipe-1', file, 'Pasta', {
      provider: 'Pexels',
      externalId: '42',
      url: 'https://www.pexels.com/photo/42',
      authorName: 'Ada',
      authorUrl: 'https://pexels.com/@ada',
    }).subscribe();
    const request = http.expectOne('/api/recipes/recipe-1/images');
    expect(request.request.body.get('sourceProvider')).toBe('Pexels');
    expect(request.request.body.get('sourceExternalId')).toBe('42');
    expect(request.request.body.get('sourceUrl')).toBe('https://www.pexels.com/photo/42');
    expect(request.request.body.get('sourceAuthorName')).toBe('Ada');
    expect(request.request.body.get('sourceAuthorUrl')).toBe('https://pexels.com/@ada');
    request.flush({});
  });

  it('updates, reorders, and deletes images through the gallery endpoints', () => {
    service.updateImage('recipe-1', 'image-1', 'New alt').subscribe();
    let request = http.expectOne('/api/recipes/recipe-1/images/image-1');
    expect(request.request.method).toBe('PATCH'); request.flush({});

    service.reorderImages('recipe-1', ['image-2', 'image-1']).subscribe();
    request = http.expectOne('/api/recipes/recipe-1/images/order');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ imageIds: ['image-2', 'image-1'] }); request.flush(null);

    service.deleteImage('recipe-1', 'image-1').subscribe();
    request = http.expectOne('/api/recipes/recipe-1/images/image-1');
    expect(request.request.method).toBe('DELETE'); request.flush(null);
  });

  it('starts, polls, and accepts a recipe import', () => {
    service.startImport('https://youtu.be/test').subscribe();
    let request = http.expectOne('/api/recipes/import');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ url: 'https://youtu.be/test' });
    request.flush({ id: 'job-1' });

    service.getImport('job-1').subscribe();
    request = http.expectOne('/api/recipes/import/job-1');
    expect(request.request.method).toBe('GET');
    request.flush({ id: 'job-1', status: 0, draft: null, errorReason: null });

    const draft = { title: 'Soup', servings: 2, instructionsMarkdown: 'Cook.', gaps: [], ingredients: [], duplicateMatches: [] };
    service.acceptImport('job-1', draft).subscribe();
    request = http.expectOne('/api/recipes/import/job-1/accept');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBe(draft);
    request.flush({ id: 'recipe-1' });
  });
});
