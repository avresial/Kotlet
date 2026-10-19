import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of, Subject, throwError } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  Product,
  ProductLookupResult,
  ProductRequest,
  ProductStock,
  unsavedProductId,
} from '../../product.models';
import { ProductService } from '../../product.service';
import { ProductPantry } from './product-pantry';

const barcode = '04006381333931';
const typedCode = '4006381333931';

const product = (overrides: Partial<Product> = {}): Product => ({
  id: 'product-1',
  barcode,
  name: 'Oat drink',
  brand: 'Oatly',
  packageDescription: 'Carton',
  packageQuantity: 250,
  unit: 'ml',
  ingredientId: null,
  caloriesPer100: null,
  proteinPer100: null,
  carbsPer100: null,
  fatPer100: null,
  nutritionBasis: null,
  source: 'Manual',
  sourceUrl: null,
  sourceRetrievedAtUtc: null,
  ...overrides,
});

const offDraft = (): Product =>
  product({
    id: unsavedProductId,
    source: 'OpenFoodFacts',
    sourceUrl: 'https://world.openfoodfacts.org/product/4006381333931',
  });

const lot = (overrides: Partial<ProductStock> = {}): ProductStock => ({
  id: 'lot-1',
  product: product(),
  quantity: 750,
  expirationDate: null,
  storageLocation: null,
  ...overrides,
});

describe('ProductPantry', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  function createPantry(
    options: {
      products?: Product[];
      stock?: ProductStock[];
      lookup?: (code: string) => Observable<ProductLookupResult>;
    } = {},
  ) {
    const service = {
      getProducts: vi.fn(() => of(options.products ?? [])),
      getStock: vi.fn(() => of(options.stock ?? [])),
      lookup: vi.fn(
        options.lookup ??
          (() => of({ status: 'open_food_facts', product: offDraft() } as ProductLookupResult)),
      ),
      saveProduct: vi.fn((_code: string, request: ProductRequest) =>
        of(product({ id: 'saved-1', name: request.name })),
      ),
      addStock: vi.fn(() => of(lot({ id: 'lot-new' }))),
      updateStock: vi.fn(() => of(lot())),
      deleteStock: vi.fn(() => of(undefined)),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: ProductService, useValue: service },
        {
          provide: TranslationService,
          useValue: { language: signal('en'), translate: (key: string) => key },
        },
      ],
    });
    const pantry = TestBed.runInInjectionContext(() => new ProductPantry());
    pantry.ngOnInit();
    return { pantry, service };
  }

  function enterCode(pantry: ProductPantry, code: string): void {
    pantry.lookupForm.controls.code.setValue(code);
  }

  describe('lookup', () => {
    it('rejects an invalid code without calling the backend', () => {
      const { pantry, service } = createPantry();
      enterCode(pantry, '4006381333932');

      pantry.lookup();

      expect(pantry.codeError()).toBe('pantry.product.codeInvalid');
      expect(service.lookup).not.toHaveBeenCalled();
      expect(pantry.draft()).toBeNull();
    });

    it('prefills an editable Open Food Facts draft and saves nothing yet', () => {
      const { pantry, service } = createPantry();
      enterCode(pantry, typedCode);

      pantry.lookup();

      expect(service.lookup).toHaveBeenCalledWith(barcode);
      expect(pantry.lookupStatus()).toBe('open_food_facts');
      expect(pantry.draft()?.isSaved).toBe(false);
      expect(pantry.productForm.controls.name.value).toBe('Oat drink');
      expect(pantry.productForm.controls.unit.enabled).toBe(true);
      expect(service.saveProduct).not.toHaveBeenCalled();
      expect(service.addStock).not.toHaveBeenCalled();
    });

    it('reuses the saved catalog on a repeated lookup instead of asking the backend', () => {
      const saved = product();
      const { pantry, service } = createPantry({ products: [saved] });
      enterCode(pantry, typedCode);

      pantry.lookup();

      expect(service.lookup).not.toHaveBeenCalled();
      expect(pantry.lookupStatus()).toBe('local');
      expect(pantry.draft()?.isSaved).toBe(true);
      expect(pantry.productForm.controls.unit.enabled).toBe(true);
      expect(pantry.productForm.controls.packageQuantity.enabled).toBe(true);
    });

    it.each(['not_found', 'unavailable', 'rate_limited'] as const)(
      'opens a blank manual draft for %s',
      (status) => {
        const { pantry } = createPantry({ lookup: () => of({ status, product: null }) });
        enterCode(pantry, typedCode);

        pantry.lookup();

        expect(pantry.lookupStatus()).toBe(status);
        expect(pantry.draft()?.barcode).toBe(barcode);
        expect(pantry.productForm.controls.name.value).toBe('');
        expect(pantry.draft()?.product.source).toBe('Manual');
      },
    );

    it('keeps manual entry available when the lookup request fails', () => {
      const { pantry } = createPantry({
        lookup: () => throwError(() => new HttpErrorResponse({ status: 503 })),
      });
      enterCode(pantry, typedCode);

      pantry.lookup();

      expect(pantry.lookupStatus()).toBe('unavailable');
      expect(pantry.draft()).not.toBeNull();
    });

    it('shows the validation error and no draft when the backend rejects the code', () => {
      const { pantry } = createPantry({
        lookup: () =>
          throwError(
            () => new HttpErrorResponse({ status: 400, error: { detail: 'Bad checksum' } }),
          ),
      });
      enterCode(pantry, typedCode);

      pantry.lookup();

      expect(pantry.codeError()).toBe('Bad checksum');
      expect(pantry.draft()).toBeNull();
    });

    it('ignores a late reply after the code was edited', () => {
      const reply = new Subject<ProductLookupResult>();
      const { pantry } = createPantry({ lookup: () => reply });
      enterCode(pantry, typedCode);
      pantry.lookup();
      expect(pantry.isLookingUp()).toBe(true);

      enterCode(pantry, '4006381333');
      reply.next({ status: 'open_food_facts', product: offDraft() });

      expect(pantry.isLookingUp()).toBe(false);
      expect(pantry.draft()).toBeNull();
      expect(pantry.lookupStatus()).toBeNull();
    });

    it('lets a newer lookup win over an older reply', () => {
      const replies: Subject<ProductLookupResult>[] = [];
      const { pantry } = createPantry({
        lookup: () => {
          const reply = new Subject<ProductLookupResult>();
          replies.push(reply);
          return reply;
        },
      });
      enterCode(pantry, typedCode);
      pantry.lookup();
      enterCode(pantry, '96385074');
      pantry.lookup();

      replies[0].next({ status: 'open_food_facts', product: offDraft() });
      replies[1].next({ status: 'not_found', product: null });

      expect(pantry.draft()?.barcode).toBe('00000096385074');
      expect(pantry.lookupStatus()).toBe('not_found');
    });

    it('invalidates the draft as soon as the code is edited', () => {
      const { pantry } = createPantry();
      enterCode(pantry, typedCode);
      pantry.lookup();
      expect(pantry.draft()).not.toBeNull();

      enterCode(pantry, `${typedCode}1`);

      expect(pantry.draft()).toBeNull();
    });

    it('does not start a second lookup while one is running', () => {
      const { pantry, service } = createPantry({ lookup: () => new Subject() });
      enterCode(pantry, typedCode);

      pantry.lookup();
      pantry.lookup();

      expect(service.lookup).toHaveBeenCalledOnce();
    });

    it('looks up codes delivered by the scanner', () => {
      const { pantry, service } = createPantry();

      pantry.onCodeDetected(typedCode);

      expect(pantry.lookupForm.controls.code.value).toBe(typedCode);
      expect(service.lookup).toHaveBeenCalledWith(barcode);
    });
  });

  describe('saving', () => {
    function draftFromLookup(options: Parameters<typeof createPantry>[0] = {}) {
      const created = createPantry(options);
      enterCode(created.pantry, typedCode);
      created.pantry.lookup();
      return created;
    }

    it('saves the catalog entry then adds stock by packages in the product unit', () => {
      const { pantry, service } = draftFromLookup();
      pantry.stockForm.patchValue({
        entryMode: 'packages',
        packages: 3,
        expirationDate: '2026-12-01',
        storageLocation: 2,
      });

      expect(pantry.stockQuantity()).toBe(750);
      pantry.saveProductAndAddStock();

      expect(service.saveProduct).toHaveBeenCalledOnce();
      expect(service.addStock).toHaveBeenCalledWith({
        productId: 'saved-1',
        quantity: 750,
        expirationDate: '2026-12-01',
        storageLocation: 2,
      });
      expect(pantry.stock().map((item) => item.id)).toEqual(['lot-new']);
      expect(pantry.success()).toBe('pantry.product.savedWithStock');
      expect(pantry.draft()).toBeNull();
    });

    it('adds a known product without rewriting unchanged catalog data', () => {
      const { pantry, service } = draftFromLookup({ products: [product()] });
      pantry.saveProductAndAddStock();
      expect(service.saveProduct).not.toHaveBeenCalled();
      expect(service.addStock).toHaveBeenCalledWith(
        expect.objectContaining({ productId: 'product-1', quantity: 250 }),
      );
    });

    it('requires whole packages and a nonzero rounded amount', () => {
      const { pantry, service } = draftFromLookup();
      pantry.stockForm.patchValue({ entryMode: 'packages', packages: 0.5 });
      pantry.saveProductAndAddStock();
      expect(pantry.stockQuantity()).toBeNull();
      pantry.stockForm.patchValue({ entryMode: 'amount', amount: 0.00001 });
      pantry.saveProductAndAddStock();
      expect(pantry.stockQuantity()).toBeNull();
      expect(service.addStock).not.toHaveBeenCalled();
    });

    it('adds a raw amount without multiplying by the package quantity', () => {
      const { pantry, service } = draftFromLookup();
      pantry.stockForm.patchValue({ entryMode: 'amount', amount: 120.5 });

      pantry.saveProductAndAddStock();

      expect(service.addStock).toHaveBeenCalledWith(
        expect.objectContaining({ quantity: 120.5, expirationDate: null, storageLocation: null }),
      );
    });

    it('sends corrected values, keeps unknown nutrition null and preserves Open Food Facts source', () => {
      const { pantry, service } = draftFromLookup();
      pantry.productForm.patchValue({
        name: '  Oat drink barista ',
        brand: '  ',
        caloriesPer100: 0,
        proteinPer100: null,
        nutritionBasis: 'ml',
      });

      pantry.saveProductOnly();

      expect(service.saveProduct).toHaveBeenCalledWith(barcode, {
        name: 'Oat drink barista',
        brand: null,
        packageDescription: 'Carton',
        packageQuantity: 250,
        unit: 'ml',
        ingredientId: null,
        caloriesPer100: 0,
        proteinPer100: null,
        carbsPer100: null,
        fatPer100: null,
        nutritionBasis: 'ml',
        source: 'OpenFoodFacts',
      });
      expect(service.addStock).not.toHaveBeenCalled();
      expect(pantry.success()).toBe('pantry.product.saved');
    });

    it('drops the nutrition basis when no nutrition value is given', () => {
      const { pantry, service } = draftFromLookup();
      pantry.productForm.patchValue({ nutritionBasis: 'g' });

      pantry.saveProductOnly();

      expect(service.saveProduct.mock.calls[0][1].nutritionBasis).toBeNull();
    });

    it('requires a nutrition basis when a nutrition value is entered', () => {
      const { pantry, service } = draftFromLookup();
      pantry.productForm.patchValue({ proteinPer100: 3, nutritionBasis: '' });

      pantry.saveProductOnly();

      expect(pantry.productForm.hasError('nutritionBasisRequired')).toBe(true);
      expect(service.saveProduct).not.toHaveBeenCalled();
    });

    it('does not save when the name is missing', () => {
      const { pantry, service } = draftFromLookup({
        lookup: () => of({ status: 'not_found', product: null }),
      });

      pantry.saveProductAndAddStock();

      expect(service.saveProduct).not.toHaveBeenCalled();
      expect(pantry.productForm.controls.name.touched).toBe(true);
    });

    it('does not add stock for a zero quantity', () => {
      const { pantry, service } = draftFromLookup();
      pantry.stockForm.patchValue({ entryMode: 'amount', amount: 0 });

      pantry.saveProductAndAddStock();

      expect(pantry.stockQuantity()).toBeNull();
      expect(service.saveProduct).not.toHaveBeenCalled();
    });

    it('ignores duplicate submissions while a save is in flight', () => {
      const pending = new Subject<Product>();
      const { pantry, service } = draftFromLookup();
      service.saveProduct.mockReturnValueOnce(pending);

      pantry.saveProductAndAddStock();
      pantry.saveProductAndAddStock();
      pantry.saveProductOnly();

      expect(pantry.isSubmitting()).toBe(true);
      expect(service.saveProduct).toHaveBeenCalledOnce();
    });

    it('retries a failed stock add without saving the catalog entry again', () => {
      const { pantry, service } = draftFromLookup();
      service.addStock.mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 500 })),
      );

      pantry.saveProductAndAddStock();

      expect(pantry.error()).toBe('pantry.product.stockError');
      expect(pantry.draft()).not.toBeNull();
      expect(pantry.isSubmitting()).toBe(false);
      expect(pantry.productForm.controls.unit.enabled).toBe(true);

      pantry.saveProductAndAddStock();

      expect(service.saveProduct).toHaveBeenCalledOnce();
      expect(service.addStock).toHaveBeenCalledTimes(2);
      expect(pantry.draft()).toBeNull();
    });

    it('saves again when the form was edited after a failed stock add', () => {
      const { pantry, service } = draftFromLookup();
      service.addStock.mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 500 })),
      );
      pantry.saveProductAndAddStock();

      pantry.productForm.patchValue({ name: 'Corrected name' });
      pantry.saveProductAndAddStock();

      expect(service.saveProduct).toHaveBeenCalledTimes(2);
    });

    it('shows the API error when saving the catalog entry fails', () => {
      const { pantry, service } = draftFromLookup();
      service.saveProduct.mockReturnValueOnce(
        throwError(
          () => new HttpErrorResponse({ status: 409, error: { detail: 'Stock uses this unit' } }),
        ),
      );

      pantry.saveProductAndAddStock();

      expect(pantry.error()).toBe('Stock uses this unit');
      expect(pantry.draft()).not.toBeNull();
      expect(service.addStock).not.toHaveBeenCalled();
    });

    it('does not add duplicate rows when the backend increments an existing lot', () => {
      const existing = lot({ id: 'lot-1', quantity: 250 });
      const { pantry, service } = draftFromLookup({ stock: [existing] });
      service.addStock.mockReturnValueOnce(of(lot({ id: 'lot-1', quantity: 500 })));

      pantry.saveProductAndAddStock();

      expect(pantry.stock()).toHaveLength(1);
      expect(pantry.stock()[0].quantity).toBe(500);
    });

    it('adds the saved product to the catalog, including products without stock', () => {
      const { pantry } = draftFromLookup();

      pantry.saveProductOnly();

      expect(pantry.products().map((item) => item.id)).toEqual(['saved-1']);
      expect(pantry.stockTotals().get('saved-1')).toBeUndefined();
    });

    it('does not let a save finishing after a code edit reopen or alter the new draft', () => {
      const pending = new Subject<Product>();
      const { pantry, service } = draftFromLookup();
      service.saveProduct.mockReturnValueOnce(pending);
      pantry.saveProductOnly();

      enterCode(pantry, '96385074');
      pending.next(product({ id: 'saved-1' }));
      pending.complete();

      expect(pantry.draft()).toBeNull();
      expect(pantry.savedProduct()).toBeNull();
      expect(pantry.isSubmitting()).toBe(false);
    });
  });

  describe('catalog editing', () => {
    it('opens a saved product for correction with the unit locked', () => {
      const saved = product({ source: 'OpenFoodFacts' });
      const { pantry, service } = createPantry({ products: [saved], stock: [lot()] });
      service.saveProduct.mockReturnValueOnce(
        of(product({ name: 'Renamed', source: 'OpenFoodFacts' })),
      );

      pantry.editProduct(saved);
      pantry.productForm.patchValue({ name: 'Renamed' });
      pantry.saveProductOnly();

      expect(pantry.productForm.controls.unit.disabled).toBe(true);
      expect(service.saveProduct).toHaveBeenCalledWith(
        barcode,
        expect.objectContaining({ name: 'Renamed', unit: 'ml', source: 'OpenFoodFacts' }),
      );
    });

    it('filters the catalog by name, brand and barcode', () => {
      const { pantry } = createPantry({
        products: [
          product({ id: 'a', name: 'Oat drink' }),
          product({ id: 'b', name: 'Rice', brand: 'Sunwheat', barcode: '00000096385074' }),
        ],
      });

      pantry.catalogSearch.set('sunw');
      expect(pantry.filteredProducts().map((item) => item.id)).toEqual(['b']);
      pantry.catalogSearch.set('96385074');
      expect(pantry.filteredProducts().map((item) => item.id)).toEqual(['b']);
      pantry.catalogSearch.set('');
      expect(pantry.filteredProducts()).toHaveLength(2);
    });

    it('totals stock per product across lots', () => {
      const { pantry } = createPantry({
        products: [product()],
        stock: [lot({ id: 'l1', quantity: 250 }), lot({ id: 'l2', quantity: 500 })],
      });

      expect(pantry.stockTotals().get('product-1')).toBe(750);
    });
  });

  describe('stock lots', () => {
    it('updates quantity, date and location with PUT', () => {
      const existing = lot();
      const { pantry, service } = createPantry({ stock: [existing] });
      service.updateStock.mockReturnValueOnce(
        of(lot({ quantity: 500, expirationDate: '2026-11-01', storageLocation: 1 })),
      );
      pantry.startLotEdit(existing);
      pantry.lotForm.patchValue({
        quantity: 500,
        expirationDate: '2026-11-01',
        storageLocation: 1,
      });

      pantry.saveLot(existing);

      expect(service.updateStock).toHaveBeenCalledWith('lot-1', {
        quantity: 500,
        expirationDate: '2026-11-01',
        storageLocation: 1,
      });
      expect(pantry.stock()[0].quantity).toBe(500);
      expect(pantry.editingLotId()).toBeNull();
    });

    it('shows the API conflict and keeps editing when the target lot already exists', () => {
      const existing = lot();
      const { pantry, service } = createPantry({ stock: [existing] });
      service.updateStock.mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 409, error: { detail: 'Lot exists' } })),
      );
      pantry.startLotEdit(existing);

      pantry.saveLot(existing);

      expect(pantry.error()).toBe('Lot exists');
      expect(pantry.editingLotId()).toBe('lot-1');
      expect(pantry.stock()).toHaveLength(1);
    });

    it('rejects a negative quantity', () => {
      const existing = lot();
      const { pantry, service } = createPantry({ stock: [existing] });
      pantry.startLotEdit(existing);
      pantry.lotForm.patchValue({ quantity: -1 });

      pantry.saveLot(existing);

      expect(service.updateStock).not.toHaveBeenCalled();
    });

    it('deletes only the confirmed lot', () => {
      const keep = lot({ id: 'lot-2' });
      const { pantry, service } = createPantry({ stock: [lot(), keep] });
      vi.spyOn(window, 'confirm').mockReturnValue(true);

      pantry.removeLot(pantry.stock()[0]);

      expect(service.deleteStock).toHaveBeenCalledWith('lot-1');
      expect(pantry.stock().map((item) => item.id)).toEqual(['lot-2']);
    });

    it('keeps the lot when the removal is not confirmed', () => {
      const { pantry, service } = createPantry({ stock: [lot()] });
      vi.spyOn(window, 'confirm').mockReturnValue(false);

      pantry.removeLot(pantry.stock()[0]);

      expect(service.deleteStock).not.toHaveBeenCalled();
      expect(pantry.stock()).toHaveLength(1);
    });

    it('keeps the lot and reports an error when removal fails', () => {
      const { pantry, service } = createPantry({ stock: [lot()] });
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      service.deleteStock.mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 500 })),
      );

      pantry.removeLot(pantry.stock()[0]);

      expect(pantry.error()).toBe('pantry.product.lotRemoveError');
      expect(pantry.stock()).toHaveLength(1);
      expect(pantry.busyLotId()).toBeNull();
    });
  });

  describe('loading and attribution', () => {
    it('reports a load failure', () => {
      const { pantry, service } = createPantry();
      service.getProducts.mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 500 })),
      );

      pantry.ngOnInit();

      expect(pantry.error()).toBe('pantry.product.loadError');
      expect(pantry.isLoading()).toBe(false);
    });

    it('links only to Open Food Facts hosts', () => {
      const { pantry } = createPantry();

      expect(pantry.sourceLink(offDraft())).toBe(
        'https://world.openfoodfacts.org/product/4006381333931',
      );
      expect(pantry.sourceLink(product({ sourceUrl: 'javascript:alert(1)' }))).toBe(
        `https://world.openfoodfacts.org/product/${barcode}`,
      );
      expect(
        pantry.sourceLink(product({ sourceUrl: 'https://evil.example/openfoodfacts.org' })),
      ).toBe(`https://world.openfoodfacts.org/product/${barcode}`);
    });
  });
});
