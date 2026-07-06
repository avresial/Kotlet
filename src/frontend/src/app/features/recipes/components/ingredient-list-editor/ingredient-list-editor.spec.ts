import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { IngredientListEditor } from './ingredient-list-editor';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../../ingredients/ingredient.models';

const buildIngredient = (overrides: Partial<Ingredient>): Ingredient => ({
  id: 'ingredient-1',
  name: 'Chicken egg',
  defaultName: 'Chicken egg',
  translation: null,
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
  createdAtUtc: '2026-01-01T00:00:00Z',
  ...overrides,
});

describe('IngredientListEditor default unit', () => {
  function createEditor(ingredients: Ingredient[]): IngredientListEditor {
    TestBed.configureTestingModule({
      imports: [IngredientListEditor],
      providers: [
        { provide: IngredientService, useValue: { getAll: () => of(ingredients) } },
        { provide: TranslationService, useValue: { translate: (key: string) => key } },
      ],
    });
    const fixture = TestBed.createComponent(IngredientListEditor);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  function selectByName(editor: IngredientListEditor, name: string) {
    editor.writeValue([]);
    const row = editor.rows[0];
    row.get('name')?.setValue(name);
    editor.selectIngredient(row);
    return row;
  }

  it('defaults to piece for a countable ingredient with a piece size', () => {
    const editor = createEditor([
      buildIngredient({ isCountable: true, measurementUnitsPerPiece: 50 }),
    ]);

    const row = selectByName(editor, 'Chicken egg');

    expect(row.get('unit')?.value).toBe('piece');
  });

  it('falls back to the base unit for a countable ingredient without a piece size', () => {
    const editor = createEditor([
      buildIngredient({ isCountable: true, measurementUnitsPerPiece: null }),
    ]);

    const row = selectByName(editor, 'Chicken egg');

    expect(row.get('unit')?.value).toBe('g');
  });

  it('keeps the base unit for a non-countable ingredient', () => {
    const editor = createEditor([
      buildIngredient({ name: 'Wheat flour', defaultName: 'Wheat flour' }),
    ]);

    const row = selectByName(editor, 'Wheat flour');

    expect(row.get('unit')?.value).toBe('g');
  });

  it('clears the unit when no ingredient matches the typed name', () => {
    const editor = createEditor([buildIngredient({})]);

    const row = selectByName(editor, 'Unknown ingredient');

    expect(row.get('unit')?.value).toBeNull();
  });
});
