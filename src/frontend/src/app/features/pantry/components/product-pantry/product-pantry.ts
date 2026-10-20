import { NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  Injector,
  inject,
  input,
  OnDestroy,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { finalize, forkJoin, map, Observable, of, Subscription, switchMap, tap } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { IngredientPicker } from '../../../ingredients/components/ingredient-picker/ingredient-picker';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { toGtin14 } from '../../barcode';
import { storageLocations } from '../../pantry.models';
import {
  NutritionBasis,
  nutritionBases,
  odblLicenseUrl,
  openFoodFactsHost,
  openFoodFactsProductBaseUrl,
  Product,
  ProductLookupResult,
  ProductLookupStatus,
  ProductRequest,
  ProductStock,
  ProductUnit,
  productUnits,
  StorageLocationValue,
  unsavedProductId,
} from '../../product.models';
import { ProductService } from '../../product.service';
import { ProductBarcodeScanner } from '../product-barcode-scanner/product-barcode-scanner';

export type StockEntryMode = 'packages' | 'amount';
type SubmitPlan = 'catalogOnly' | 'catalogAndStock';

interface ProductDraft {
  /** Identifies one draft session; unchanged when the draft object is updated after a save. */
  key: number;
  /** Canonical GTIN-14 the draft belongs to. */
  barcode: string;
  product: Product;
  /** Saved products keep their unit and package quantity so existing stock keeps its meaning. */
  isSaved: boolean;
}

function roundQuantity(value: number): number {
  return Math.round(value * 1000) / 1000;
}

function blankToNull(value: string | null): string | null {
  const trimmed = (value ?? '').trim();
  return trimmed === '' ? null : trimmed;
}

function blankProduct(barcode: string): Product {
  return {
    id: unsavedProductId,
    barcode,
    name: '',
    brand: null,
    packageDescription: null,
    packageQuantity: 1,
    unit: 'pcs',
    ingredientId: null,
    caloriesPer100: null,
    proteinPer100: null,
    carbsPer100: null,
    fatPer100: null,
    nutritionBasis: null,
    source: 'Manual',
    sourceUrl: null,
    sourceRetrievedAtUtc: null,
  };
}

function isSavedProduct(product: Product): boolean {
  return product.id !== '' && product.id !== unsavedProductId;
}

/** Nutrition values are meaningless without knowing whether they describe 100 g or 100 ml. */
function nutritionBasisValidator(group: AbstractControl): ValidationErrors | null {
  const { caloriesPer100, proteinPer100, carbsPer100, fatPer100, nutritionBasis } = group.value;
  const hasNutrition = [caloriesPer100, proteinPer100, carbsPer100, fatPer100].some(
    (value) => value !== null && value !== undefined,
  );
  return hasNutrition && !nutritionBasis ? { nutritionBasisRequired: true } : null;
}

function upsertById<T extends { id: string }>(items: T[], item: T): T[] {
  return items.some((current) => current.id === item.id)
    ? items.map((current) => (current.id === item.id ? item : current))
    : [...items, item];
}

@Component({
  selector: 'app-product-pantry',
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    IngredientPicker,
    ProductBarcodeScanner,
    TranslatePipe,
  ],
  templateUrl: './product-pantry.html',
  styleUrl: './product-pantry.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductPantry implements OnInit, OnDestroy {
  private readonly productService = inject(ProductService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly translations = inject(TranslationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly nameInput = viewChild<ElementRef<HTMLInputElement>>('productName');
  private readonly lotQuantityInput = viewChild<ElementRef<HTMLInputElement>>('lotQuantity');

  readonly ingredients = input<readonly Ingredient[]>([]);

  readonly products = signal<Product[]>([]);
  readonly stock = signal<ProductStock[]>([]);
  readonly isLoading = signal(true);
  readonly isOpen = signal(false);
  readonly isLookingUp = signal(false);
  readonly isSubmitting = signal(false);
  readonly busyLotId = signal<string | null>(null);
  readonly editingLotId = signal<string | null>(null);
  readonly lookupStatus = signal<ProductLookupStatus | null>(null);
  readonly draft = signal<ProductDraft | null>(null);
  /** The catalog entry already persisted for the current draft, so a failed stock add can retry without a second save. */
  readonly savedProduct = signal<Product | null>(null);
  readonly codeError = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly catalogSearch = signal('');

  readonly locations = storageLocations;
  readonly units = productUnits;
  readonly nutritionBases = nutritionBases;
  readonly offLicenseUrl = odblLicenseUrl;

  readonly lookupForm = this.formBuilder.nonNullable.group({ code: [''] });
  readonly productForm = this.formBuilder.nonNullable.group(
    {
      name: ['', [Validators.required, Validators.maxLength(200)]],
      brand: [''],
      packageDescription: [''],
      packageQuantity: [1 as number | null, [Validators.required, Validators.min(0.001)]],
      unit: ['pcs' as ProductUnit, Validators.required],
      ingredientId: [''],
      caloriesPer100: [null as number | null, Validators.min(0)],
      proteinPer100: [null as number | null, Validators.min(0)],
      carbsPer100: [null as number | null, Validators.min(0)],
      fatPer100: [null as number | null, Validators.min(0)],
      nutritionBasis: ['' as NutritionBasis | ''],
    },
    { validators: nutritionBasisValidator },
  );
  readonly stockForm = this.formBuilder.nonNullable.group({
    entryMode: ['packages' as StockEntryMode],
    packages: [1 as number | null, Validators.min(1)],
    amount: [null as number | null, Validators.min(0.001)],
    expirationDate: [null as string | null],
    storageLocation: [null as StorageLocationValue | null],
  });
  readonly lotForm = this.formBuilder.nonNullable.group({
    quantity: [0 as number | null, [Validators.required, Validators.min(0)]],
    expirationDate: [null as string | null],
    storageLocation: [null as StorageLocationValue | null],
  });

  readonly sortedStock = computed(() =>
    [...this.stock()].sort(
      (a, b) =>
        a.product.name.localeCompare(b.product.name) ||
        (a.expirationDate ?? '9999-12-31').localeCompare(b.expirationDate ?? '9999-12-31'),
    ),
  );
  readonly stockTotals = computed(() => {
    const totals = new Map<string, number>();
    for (const lot of this.stock()) {
      totals.set(lot.product.id, roundQuantity((totals.get(lot.product.id) ?? 0) + lot.quantity));
    }
    return totals;
  });
  readonly filteredProducts = computed(() => {
    const term = this.catalogSearch().trim().toLowerCase();
    return [...this.products()]
      .filter(
        (product) =>
          term === '' ||
          [product.name, product.brand ?? '', product.barcode].some((value) =>
            value.toLowerCase().includes(term),
          ),
      )
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  private lookupSubscription: Subscription | null = null;
  /** Incremented on every code edit and lookup so replies for an older code are ignored. */
  private lookupToken = 0;
  private draftKey = 0;

  constructor() {
    this.lookupForm.controls.code.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.invalidateDraft());
    // Any edit after the catalog save means the persisted entry no longer matches the form.
    this.productForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.savedProduct.set(null));
  }

  ngOnInit(): void {
    forkJoin({ products: this.productService.getProducts(), stock: this.productService.getStock() })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ products, stock }) => {
          this.products.set(products);
          this.stock.set(stock);
        },
        error: (error) => this.error.set(this.apiError(error, 'pantry.product.loadError')),
      });
  }

  ngOnDestroy(): void {
    this.lookupSubscription?.unsubscribe();
  }

  toggle(): void {
    this.isOpen.update((isOpen) => !isOpen);
  }

  lookup(refreshNutrition = false): void {
    if (this.isLookingUp()) {
      return;
    }
    const barcode = toGtin14(this.lookupForm.controls.code.value);
    if (!barcode) {
      this.codeError.set(this.translations.translate('pantry.product.codeInvalid'));
      return;
    }

    this.codeError.set(null);
    this.error.set(null);
    this.success.set(null);
    const cached = this.products().find((product) => product.barcode === barcode);
    if (cached && !refreshNutrition) {
      this.lookupStatus.set('local');
      this.openDraft(cached, barcode);
      return;
    }

    const token = ++this.lookupToken;
    this.isLookingUp.set(true);
    const request = refreshNutrition
      ? this.productService.lookup(barcode, true)
      : this.productService.lookup(barcode);
    this.lookupSubscription = request.subscribe({
      next: (result) => {
        if (token === this.lookupToken) {
          this.isLookingUp.set(false);
          this.applyLookupResult(result, barcode);
          if (refreshNutrition && this.draft()?.isSaved) {
            // Enrichment is a reviewable draft, not a persisted catalog correction yet.
            this.savedProduct.set(null);
          }
        }
      },
      error: (error: unknown) => {
        if (token === this.lookupToken) {
          this.isLookingUp.set(false);
          this.applyLookupFailure(error, barcode);
        }
      },
    });
  }

  onCodeDetected(code: string): void {
    this.lookupForm.controls.code.setValue(code);
    this.lookup();
  }

  editProduct(product: Product): void {
    this.error.set(null);
    this.success.set(null);
    this.invalidateDraft();
    this.lookupForm.controls.code.setValue(product.barcode, { emitEvent: false });
    this.isOpen.set(true);
    this.lookupStatus.set('local');
    this.openDraft(product, product.barcode);
  }

  cancelDraft(): void {
    this.invalidateDraft();
    this.lookupForm.controls.code.setValue('', { emitEvent: false });
  }

  saveProductOnly(): void {
    this.submit('catalogOnly');
  }

  saveProductAndAddStock(): void {
    this.submit('catalogAndStock');
  }

  /** Quantity in the product's base unit that the stock part of the form would add, or null when invalid. */
  stockQuantity(): number | null {
    const { entryMode, packages, amount } = this.stockForm.getRawValue();
    const packageQuantity = Number(this.productForm.controls.packageQuantity.value);
    const quantity = entryMode === 'packages' ? (packages ?? 0) * packageQuantity : (amount ?? 0);
    if (entryMode === 'packages' && (!Number.isInteger(packages) || (packages ?? 0) < 1)) {
      return null;
    }
    const rounded = roundQuantity(quantity);
    return Number.isFinite(quantity) && rounded > 0 && rounded <= 99999999.999 ? rounded : null;
  }

  startLotEdit(lot: ProductStock): void {
    this.lotForm.reset({
      quantity: lot.quantity,
      expirationDate: lot.expirationDate,
      storageLocation: lot.storageLocation,
    });
    this.editingLotId.set(lot.id);
    afterNextRender(() => this.lotQuantityInput()?.nativeElement.focus(), {
      injector: this.injector,
    });
  }

  cancelLotEdit(): void {
    this.editingLotId.set(null);
  }

  saveLot(lot: ProductStock): void {
    if (this.busyLotId()) {
      return;
    }
    if (this.lotForm.invalid) {
      this.lotForm.markAllAsTouched();
      return;
    }
    const { quantity, expirationDate, storageLocation } = this.lotForm.getRawValue();
    this.busyLotId.set(lot.id);
    this.error.set(null);
    this.success.set(null);
    this.productService
      .updateStock(lot.id, {
        quantity: roundQuantity(Number(quantity)),
        expirationDate: expirationDate || null,
        storageLocation,
      })
      .pipe(finalize(() => this.busyLotId.set(null)))
      .subscribe({
        next: (updated) => {
          this.stock.update((lots) => upsertById(lots, updated));
          this.editingLotId.set(null);
          this.success.set(this.translations.translate('pantry.product.lotUpdated'));
        },
        error: (error) => this.error.set(this.apiError(error, 'pantry.product.lotUpdateError')),
      });
  }

  removeLot(lot: ProductStock): void {
    if (this.busyLotId()) {
      return;
    }
    const message = this.translations
      .translate('pantry.product.lotRemoveConfirm')
      .replace('{name}', lot.product.name);
    if (!window.confirm(message)) {
      return;
    }
    this.busyLotId.set(lot.id);
    this.error.set(null);
    this.success.set(null);
    this.productService
      .deleteStock(lot.id)
      .pipe(finalize(() => this.busyLotId.set(null)))
      .subscribe({
        next: () => {
          this.stock.update((lots) => lots.filter((current) => current.id !== lot.id));
          if (this.editingLotId() === lot.id) {
            this.editingLotId.set(null);
          }
          this.success.set(this.translations.translate('pantry.product.lotRemoved'));
        },
        error: (error) => this.error.set(this.apiError(error, 'pantry.product.lotRemoveError')),
      });
  }

  locationLabel(value: StorageLocationValue | null): string {
    const location = this.locations.find((candidate) => candidate.value === value);
    return location ? this.translations.translate(location.label) : '';
  }

  hasNutrition(product: Product): boolean {
    return [
      product.caloriesPer100,
      product.proteinPer100,
      product.carbsPer100,
      product.fatPer100,
    ].some((value) => value !== null && value !== undefined);
  }

  canEnrichNutrition(): boolean {
    const draft = this.draft();
    return (
      !!draft?.isSaved &&
      [
        draft.product.caloriesPer100,
        draft.product.proteinPer100,
        draft.product.carbsPer100,
        draft.product.fatPer100,
      ].some((value) => value === null || value === undefined)
    );
  }

  foodDataCentralLink(product: Product): string | null {
    const id = product.foodDataCentralId;
    return typeof id === 'number' && Number.isSafeInteger(id) && id > 0 && id <= 2147483647
      ? `https://fdc.nal.usda.gov/food-details/${id}/nutrients`
      : null;
  }

  /** Only links to Open Food Facts hosts so a stored URL can never become a script link. */
  sourceLink(product: Product): string {
    const fallback = `${openFoodFactsProductBaseUrl}${product.barcode}`;
    if (!product.sourceUrl) {
      return fallback;
    }
    try {
      const url = new URL(product.sourceUrl);
      const isOpenFoodFacts =
        url.protocol === 'https:' &&
        (url.hostname === openFoodFactsHost || url.hostname.endsWith(`.${openFoodFactsHost}`));
      return isOpenFoodFacts ? url.toString() : fallback;
    } catch {
      return fallback;
    }
  }

  private submit(plan: SubmitPlan): void {
    const draft = this.draft();
    if (!draft || this.isSubmitting()) {
      return;
    }
    const addsStock = plan === 'catalogAndStock';
    const quantity = this.stockQuantity();
    if (this.productForm.invalid || (addsStock && quantity === null)) {
      this.productForm.markAllAsTouched();
      this.stockForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.error.set(null);
    this.success.set(null);
    const isCurrent = () => this.draft()?.key === draft.key;
    const { expirationDate, storageLocation } = this.stockForm.getRawValue();
    this.persistProduct(draft, isCurrent)
      .pipe(
        switchMap((product) => {
          if (!addsStock || quantity === null) {
            return of({ product, lot: null });
          }
          return this.productService
            .addStock({
              productId: product.id,
              quantity,
              expirationDate: expirationDate || null,
              storageLocation,
            })
            .pipe(map((lot) => ({ product, lot })));
        }),
        finalize(() => this.isSubmitting.set(false)),
      )
      .subscribe({
        next: ({ lot }) => {
          if (lot) {
            this.stock.update((lots) => upsertById(lots, lot));
          }
          if (isCurrent()) {
            this.success.set(
              this.translations.translate(
                lot ? 'pantry.product.savedWithStock' : 'pantry.product.saved',
              ),
            );
            this.cancelDraft();
          }
        },
        error: (error) => {
          if (isCurrent()) {
            this.error.set(
              this.apiError(
                error,
                this.savedProduct() ? 'pantry.product.stockError' : 'pantry.product.saveError',
              ),
            );
          }
        },
      });
  }

  /** Saves the catalog entry once; a retry after a failed stock add reuses the persisted entry. */
  private persistProduct(draft: ProductDraft, isCurrent: () => boolean): Observable<Product> {
    const alreadySaved = this.savedProduct();
    if (alreadySaved) {
      return of(alreadySaved);
    }
    return this.productService.saveProduct(draft.barcode, this.buildRequest(draft)).pipe(
      tap((product) => {
        this.products.update((products) => upsertById(products, product));
        // A catalog correction also changes the name/unit shown on that product's existing lots.
        this.stock.update((lots) =>
          lots.map((lot) => (lot.product.id === product.id ? { ...lot, product } : lot)),
        );
        if (isCurrent()) {
          this.savedProduct.set(product);
          this.lockSavedProductFields();
        }
      }),
    );
  }

  private buildRequest(draft: ProductDraft): ProductRequest {
    const value = this.productForm.getRawValue();
    const hasNutrition = [
      value.caloriesPer100,
      value.proteinPer100,
      value.carbsPer100,
      value.fatPer100,
    ].some((nutrient) => nutrient !== null);
    return {
      name: value.name.trim(),
      brand: blankToNull(value.brand),
      packageDescription: blankToNull(value.packageDescription),
      packageQuantity: Number(value.packageQuantity),
      unit: value.unit,
      ingredientId: value.ingredientId || null,
      caloriesPer100: value.caloriesPer100,
      proteinPer100: value.proteinPer100,
      carbsPer100: value.carbsPer100,
      fatPer100: value.fatPer100,
      nutritionBasis: hasNutrition && value.nutritionBasis ? value.nutritionBasis : null,
      // Editing never changes provenance, so the Open Food Facts attribution stays on corrected products.
      source: draft.product.source,
      ...(draft.product.foodDataCentralId != null
        ? { foodDataCentralId: draft.product.foodDataCentralId }
        : {}),
    };
  }

  private applyLookupResult(result: ProductLookupResult, barcode: string): void {
    this.lookupStatus.set(result.status);
    const hasDraftProduct =
      result.product !== null &&
      (result.status === 'local' ||
        result.status === 'open_food_facts' ||
        result.status === 'food_data_central');
    this.openDraft(
      hasDraftProduct && result.product ? result.product : blankProduct(barcode),
      barcode,
    );
  }

  private applyLookupFailure(error: unknown, barcode: string): void {
    if (error instanceof HttpErrorResponse && error.status === 400) {
      this.codeError.set(this.apiError(error, 'pantry.product.codeInvalid'));
      return;
    }
    // The lookup service being unreachable must not block entering the product by hand.
    this.lookupStatus.set('unavailable');
    this.openDraft(blankProduct(barcode), barcode);
  }

  private openDraft(product: Product, barcode: string): void {
    const isSaved = isSavedProduct(product);
    this.productForm.reset({
      name: product.name,
      brand: product.brand ?? '',
      packageDescription: product.packageDescription ?? '',
      packageQuantity: product.packageQuantity,
      unit: product.unit,
      ingredientId: product.ingredientId ?? '',
      caloriesPer100: product.caloriesPer100,
      proteinPer100: product.proteinPer100,
      carbsPer100: product.carbsPer100,
      fatPer100: product.fatPer100,
      nutritionBasis: product.nutritionBasis ?? '',
    });
    this.stockForm.reset({
      entryMode: 'packages',
      packages: 1,
      amount: null,
      expirationDate: null,
      storageLocation: null,
    });
    this.setProductFieldsLocked(this.stock().some((lot) => lot.product.id === product.id));
    this.savedProduct.set(isSaved ? product : null);
    this.draft.set({ key: ++this.draftKey, barcode, product, isSaved });
    afterNextRender(() => this.nameInput()?.nativeElement.focus(), { injector: this.injector });
    this.isOpen.set(true);
  }

  private lockSavedProductFields(): void {
    this.setProductFieldsLocked(
      this.stock().some((lot) => lot.product.id === this.savedProduct()?.id),
    );
    this.draft.update((draft) => (draft ? { ...draft, isSaved: true } : draft));
  }

  private setProductFieldsLocked(isLocked: boolean): void {
    const { unit, packageQuantity } = this.productForm.controls;
    for (const control of [unit, packageQuantity]) {
      if (isLocked) {
        control.disable({ emitEvent: false });
      } else {
        control.enable({ emitEvent: false });
      }
    }
  }

  /** Drops the draft and any in-flight lookup so a stale reply cannot resurrect an edited code. */
  private invalidateDraft(): void {
    this.lookupToken++;
    this.lookupSubscription?.unsubscribe();
    this.lookupSubscription = null;
    this.isLookingUp.set(false);
    this.lookupStatus.set(null);
    this.draft.set(null);
    this.savedProduct.set(null);
    this.codeError.set(null);
  }

  private apiError(error: unknown, fallbackKey: string): string {
    return getApiError(error, this.translations.translate(fallbackKey));
  }
}
