import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RecipeForm } from './recipe-form';
import { IngredientListEditor } from '../ingredient-list-editor/ingredient-list-editor';
import { TranslationService } from '../../../../core/i18n/translation.service';

describe('RecipeForm image picker', () => {
  let fixture: ComponentFixture<RecipeForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RecipeForm],
      providers: [{
        provide: TranslationService,
        useValue: { translate: (key: string) => key === 'recipes.addImage' ? 'Add image' : key },
      }],
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
});
