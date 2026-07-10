import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { RecipeForm } from './recipe-form';
import { IngredientListEditor } from '../ingredient-list-editor/ingredient-list-editor';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { RecipeImageSearchService } from '../../services/recipe-image-search.service';
import { RecipeImageImportResult } from '../../models/recipe.models';

describe('RecipeForm image picker', () => {
  let fixture: ComponentFixture<RecipeForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RecipeForm],
      providers: [{
        provide: TranslationService,
        useValue: { translate: (key: string) => key === 'recipes.addImage' ? 'Add image' : key },
      }, { provide: RecipeImageSearchService, useValue: { search: () => of([]), import: () => of({}) } }],
    }).compileComponents();
    fixture = TestBed.createComponent(RecipeForm);
    fixture.componentRef.setInput('showImagePicker', true);
    fixture.detectChanges();
  });

  it('shows an Add image text button for recipe creation', () => {
    const button = fixture.nativeElement.querySelector('.image-picker button');
    expect(button.textContent.trim()).toBe('Add image');
  });

  it('emits a supported selected image', () => {
    const file = new File(['image'], 'soup.webp', { type: 'image/webp' });
    const emitted: Array<File | null> = [];
    fixture.componentInstance.imageSelected.subscribe(value => emitted.push(value));

    fixture.componentInstance.chooseImage({
      target: { files: [file], value: 'soup.webp' },
    } as unknown as Event);

    expect(fixture.componentInstance.selectedImage).toBe(file);
    expect(emitted).toEqual([file]);
  });

  it('accepts an existing ingredient by its stable id when its display name changed', () => {
    const editor = fixture.debugElement.query(
      (debugElement) => debugElement.componentInstance instanceof IngredientListEditor,
    ).componentInstance as IngredientListEditor;
    editor.ingredients.set([{
      id: 'ingredient-1',
      name: 'Localized name',
      defaultName: 'Original name',
      translation: 'Localized name',
      measurementUnit: 'g',
      isCountable: false,
      measurementUnitsPerPiece: null,
      caloriesPer100BaseUnits: 0,
      pricePer100BaseUnits: 0,
      svgIcon: null,
      category: 0,
      allergens: 0,
      attributes: 0,
      suitability: 0,
      isAiModified: false,
      createdAtUtc: '2026-01-01T00:00:00Z',
    }]);
    editor.isLoadingIngredients.set(false);

    editor.writeValue([{
      ingredientId: 'ingredient-1',
      name: 'Original name',
      quantity: 100,
      unit: 'g',
      note: null,
    }]);

    expect(editor.validate(fixture.componentInstance.form.controls.ingredients)).toBeNull();
  });

  it('converts an imported result into a selected file and forwards its metadata', () => {
    const result: RecipeImageImportResult = {
      content: 'd2VicA==',
      contentType: 'image/webp',
      width: 1200,
      height: 800,
      provider: 'Pexels',
      externalImageId: '42',
      sourcePageUrl: 'https://www.pexels.com/photo/42',
      authorName: 'Ada',
      authorUrl: 'https://pexels.com/@ada',
      altText: 'Pasta',
    };
    const selected: Array<File | null> = [];
    const imported: RecipeImageImportResult[] = [];
    fixture.componentInstance.imageSelected.subscribe(value => selected.push(value));
    fixture.componentInstance.generatedImageImported.subscribe(value => imported.push(value));

    fixture.componentInstance.handleGeneratedImage(result);

    expect(fixture.componentInstance.selectedImage?.name).toBe('generated-recipe-image.webp');
    expect(fixture.componentInstance.selectedImage?.type).toBe('image/webp');
    expect(fixture.componentInstance.selectedImage?.size).toBe(4);
    expect(selected[0]).toBe(fixture.componentInstance.selectedImage);
    expect(imported).toEqual([result]);
  });

  it('blocks save while an image import is active and restores it afterward', () => {
    fixture.componentInstance.form.controls.title.setValue('Pasta');
    const submitted: unknown[] = [];
    fixture.componentInstance.submitted.subscribe(value => submitted.push(value));

    fixture.componentInstance.setImageImporting(true);
    fixture.componentInstance.submit();
    expect(submitted).toHaveLength(0);

    fixture.componentInstance.setImageImporting(false);
    fixture.componentInstance.submit();
    expect(submitted).toHaveLength(1);
  });
});
