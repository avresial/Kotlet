import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ShoppingListService } from './shopping-list.service';

describe('ShoppingListService', () => {
  let service: ShoppingListService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ShoppingListService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts trimmed customName and null note when creating custom item', () => {
    service.createCustom('  Paper towels  ', 2).subscribe();

    const request = http.expectOne('/api/shopping-list');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      customName: 'Paper towels',
      quantity: 2,
      note: null,
    });
    expect(request.request.body.ingredientId).toBeUndefined();
    expect(request.request.body.preparedMealId).toBeUndefined();
    request.flush({});
  });

  it('posts custom item with optional note', () => {
    service.createCustom('Dish soap', 1, 'eco friendly').subscribe();

    const request = http.expectOne('/api/shopping-list');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      customName: 'Dish soap',
      quantity: 1,
      note: 'eco friendly',
    });
    request.flush({});
  });

  it('posts ingredient creation with ingredientId', () => {
    service.create('ingredient-1', 500, 'organic').subscribe();

    const request = http.expectOne('/api/shopping-list');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      ingredientId: 'ingredient-1',
      quantity: 500,
      note: 'organic',
    });
    request.flush({});
  });

  it('posts prepared meal creation with preparedMealId', () => {
    service.createPreparedMeal('meal-1', 2, null).subscribe();

    const request = http.expectOne('/api/shopping-list');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      preparedMealId: 'meal-1',
      quantity: 2,
      note: null,
    });
    request.flush({});
  });
});
