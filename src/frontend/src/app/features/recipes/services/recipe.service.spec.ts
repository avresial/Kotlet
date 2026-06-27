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

  it('uploads a multipart image with alt text', () => {
    const file = new File(['image'], 'soup.webp', { type: 'image/webp' });
    service.uploadImage('recipe-1', file, 'Soup').subscribe();
    const request = http.expectOne('/api/recipes/recipe-1/images');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.get('file')).toBe(file);
    expect(request.request.body.get('altText')).toBe('Soup');
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
});
