import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../ingredient.models';
import { IngredientService } from '../../ingredient.service';
import { filterIngredients, IngredientsPage, paginate } from './ingredients-page';

const ingredient = (name: string, category: number): Ingredient => ({
  id: name, name, defaultName: name, translation: null, measurementUnit: 'g', isCountable: false,
  measurementUnitsPerPiece: null, caloriesPer100BaseUnits: 0, pricePer100BaseUnits: 0,
  svgIcon: null, category, allergens: 0, attributes: 0, suitability: 0, isAiModified: false, createdAtUtc: '2026-01-01T00:00:00Z',
});

describe('filterIngredients', () => {
  it('combines name and category filters and clears with null', () => {
    const items = [ingredient('Green apple', 21), ingredient('Apple pie', 90), ingredient('Pear', 21)];

    expect(filterIngredients(items, 'apple', 21).map(item => item.name)).toEqual(['Green apple']);
    expect(filterIngredients(items, '', null)).toEqual(items);
  });
});

describe('paginate', () => {
  it('slices the requested page and returns a short last page', () => {
    const items = ['a', 'b', 'c', 'd', 'e'];

    expect(paginate(items, 1, 2)).toEqual(['a', 'b']);
    expect(paginate(items, 3, 2)).toEqual(['e']);
    expect(paginate(items, 4, 2)).toEqual([]);
  });
});

describe('editor visibility', () => {
  const sample = ingredient('Chicken breast', 20);

  const createPage = (editId: string | null = null) => {
    const service = {
      getAll: vi.fn(() => of([sample])),
      autofill: vi.fn(() => of({ category: 20, allergens: 0, attributes: 0, suitability: 0 })),
      create: vi.fn(() => of(sample)),
      update: vi.fn(() => of(sample)),
      delete: vi.fn(() => of(undefined)),
    };
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => editId } } } },
        { provide: IngredientService, useValue: service },
        { provide: TranslationService, useValue: { language: signal('en'), translate: (key: string) => key } },
      ],
    });
    const page = TestBed.runInInjectionContext(() => new IngredientsPage());
    page.ngOnInit();
    return { page, service };
  };

  beforeEach(() => {
    vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('starts collapsed so the catalogue is not pushed below the fold', () => {
    const { page } = createPage();

    expect(page.isEditorOpen()).toBe(false);
  });

  it('opens the form on activation and collapses it again on request', () => {
    const { page } = createPage();

    page.openEditor();
    expect(page.isEditorOpen()).toBe(true);

    page.closeEditor();
    expect(page.isEditorOpen()).toBe(false);
  });

  it('opens the form when an existing ingredient is selected for editing', () => {
    const { page } = createPage();

    page.edit(sample);

    expect(page.isEditorOpen()).toBe(true);
    expect(page.editingId()).toBe(sample.id);
  });

  it('opens the form when the edit query parameter addresses an existing ingredient', () => {
    const { page } = createPage(sample.id);

    expect(page.isEditorOpen()).toBe(true);
    expect(page.editingId()).toBe(sample.id);
  });

  it('collapses the form again after a successful creation', () => {
    const { page, service } = createPage();
    page.openEditor();
    page.form.controls.name.setValue('Chicken breast');

    page.save();

    expect(service.create).toHaveBeenCalledOnce();
    expect(page.isEditorOpen()).toBe(false);
    expect(page.editingId()).toBeNull();
  });

  it('keeps the form open after a successful update, showing the reset creation form', () => {
    const { page, service } = createPage();
    page.edit(sample);

    page.save();

    expect(service.update).toHaveBeenCalledOnce();
    expect(page.isEditorOpen()).toBe(true);
    expect(page.editingId()).toBeNull();
  });
});
