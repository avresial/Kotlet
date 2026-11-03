import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, finalize, Observable, of, switchMap } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { IngredientPicker } from '../../../ingredients/components/ingredient-picker/ingredient-picker';
import { RecipeDetail, RecipeSummary } from '../../../recipes/models/recipe.models';
import { RecipeService } from '../../../recipes/services/recipe.service';
import { RecipePicker } from '../../../recipes/components/recipe-picker/recipe-picker';
import { PreparedMeal } from '../../../prepared-meals/prepared-meal.models';
import { PreparedMealService } from '../../../prepared-meals/prepared-meal.service';
import { MealItemPeople, ParticipantPortionChange } from '../../components/meal-item-people/meal-item-people';
import { MealPlanCopyPanel } from '../../components/meal-plan-copy-panel/meal-plan-copy-panel';
import { MealDroppedOnDay, MealPlanOverview } from '../../components/meal-plan-overview/meal-plan-overview';
import { DailyMealPlan, HouseMember, MealPlanItem, MealPlanItemType, MealPlanOverviewDay, MealSlot } from '../../models/meal-planner.models';
import { MealPlannerService } from '../../services/meal-planner.service';
import { ShoppingListIntegrationService } from '../../services/shopping-list-integration.service';
import { cachePlan, mealPlannerCacheKey, readCachedPlan } from '../../meal-plan-cache';
import { addDays, isValidDateString, todayString, weekStart } from '../../meal-plan-dates';
import {
  allocateCaloriesByPerson,
  directIngredientQuantity,
  mealCaloriesPerServing,
  recipePricePerServing,
} from '../../meal-planner-calculations';

@Component({
  selector: 'app-meal-planner-page',
  imports: [
    FormsModule, RouterLink, IngredientPicker, RecipePicker, TranslatePipe, CdkDropListGroup, CdkDropList, CdkDrag, CdkDragHandle,
    MealPlanOverview, MealPlanCopyPanel, MealItemPeople,
  ],
  templateUrl: './meal-planner-page.html',
  styleUrl: './meal-planner-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealPlannerPage implements OnInit {
  private readonly auth = inject(AuthService, { optional: true });
  private readonly service = inject(MealPlannerService);
  private readonly recipeService = inject(RecipeService);
  private readonly ingredientService = inject(IngredientService);
  private readonly preparedMealService = inject(PreparedMealService);
  private readonly shoppingListIntegration = inject(ShoppingListIntegrationService);
  private readonly translations = inject(TranslationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly initialDate = isValidDateString(this.route.snapshot.queryParamMap.get('date'))
    ? this.route.snapshot.queryParamMap.get('date')!
    : todayString();
  private lastValidDate = this.initialDate;

  readonly slots: MealSlot[] = ['breakfast', 'second-breakfast', 'dinner', 'snack', 'supper'];
  /** Second breakfast and snack are supplementary: they stay out of the way until a day actually uses them. */
  readonly optionalSlots: MealSlot[] = ['second-breakfast', 'snack'];
  readonly slotLabels = computed<Record<MealSlot, string>>(() => ({
    breakfast: this.translations.translate('meal.breakfast'),
    'second-breakfast': this.translations.translate('meal.secondBreakfast'),
    dinner: this.translations.translate('meal.lunch'),
    snack: this.translations.translate('meal.snack'),
    supper: this.translations.translate('meal.dinner'),
  }));
  /** Labels of the buttons that open a collapsed optional slot, e.g. "Add snack". */
  readonly openSlotLabels = computed<Partial<Record<MealSlot, string>>>(() => ({
    'second-breakfast': this.translations.translate('meal.addSecondBreakfast'),
    snack: this.translations.translate('meal.addSnack'),
  }));
  /** Optional slots the user opened by hand; cleared whenever the shown day changes. */
  private readonly openedSlots = signal<MealSlot[]>([]);

  readonly overviewDays = 7;
  readonly selectedDate = signal(this.initialDate);
  readonly overviewFrom = signal(weekStart(this.initialDate));
  readonly overview = signal<MealPlanOverviewDay[]>([]);
  /** Grid rows: an optional slot only earns a row when a day in the shown range plans something in it. */
  readonly overviewSlots = computed(() => this.slots.filter((slot) =>
    !this.optionalSlots.includes(slot) || this.overview().some((day) => day.plannedSlots.includes(slot))));
  readonly plan = signal<DailyMealPlan | null>(null);
  readonly isLoadingPlan = signal(false);
  readonly planError = signal<string | null>(null);

  readonly recipes = signal<RecipeSummary[]>([]);
  readonly recipeDetails = signal<Record<string, RecipeDetail>>({});
  readonly ingredients = signal<Ingredient[]>([]);
  readonly preparedMeals = signal<PreparedMeal[]>([]);
  readonly isLoadingOptions = signal(true);

  readonly members = signal<HouseMember[]>([]);

  readonly selectedRecipeId = signal<Record<MealSlot, string>>({ breakfast: '', 'second-breakfast': '', dinner: '', snack: '', supper: '' });
  readonly selectedIngredientId = signal<Record<MealSlot, string>>({ breakfast: '', 'second-breakfast': '', dinner: '', snack: '', supper: '' });
  readonly selectedPreparedMealId = signal<Record<MealSlot, string>>({ breakfast: '', 'second-breakfast': '', dinner: '', snack: '', supper: '' });
  readonly preparedAddonSelections = signal<Record<string, { selected: boolean; quantity: number }>>({});
  readonly addingSlot = signal<string | null>(null);
  readonly removingId = signal<string | null>(null);
  readonly busyItemId = signal<string | null>(null);
  readonly movingId = signal<string | null>(null);
  readonly composer = signal<Record<MealSlot, MealPlanItemType | null>>({ breakfast: null, 'second-breakfast': null, dinner: null, snack: null, supper: null });
  readonly shoppingItemState = signal<Record<string, 'adding' | 'added'>>({});
  private planRequestId = 0;

  readonly dayTotal = computed(() => this.allItems().reduce((total, item) => total + (this.itemCost(item) ?? 0), 0));
  readonly dayServings = computed(() => this.allItems().reduce((total, item) => total + item.servings, 0));
  readonly dayCalories = computed(() => this.allItems().reduce(
    (total, item) => total + (this.caloriesPerServing(item) ?? 0) * item.servings,
    0,
  ));
  readonly caloriesByPerson = computed(() =>
    allocateCaloriesByPerson(
      this.allItems(),
      this.recipeDetails(),
      this.ingredients(),
      this.preparedMeals(),
      this.translations.translate('meal.guests'),
      this.translations.translate('meal.unassignedServings'),
    )
  );

  ngOnInit(): void {
    this.loadOptions();
    this.loadMembers();

    this.route.queryParamMap.subscribe((params) => {
      const dateParam = params.get('date');
      if (!isValidDateString(dateParam)) {
        this.navigateToDate(this.selectedDate(), true);
        return;
      }
      this.applyDate(dateParam);
    });
    this.loadOverview();
  }

  loadOverview(): void {
    this.service.getOverview(this.overviewFrom(), this.overviewDays).subscribe({
      next: (days) => this.overview.set(days),
      error: () => this.overview.set([]),
    });
  }

  /** Keeps the desktop grid on the calendar week containing the selected mobile day. */
  private centerOverviewOnSelected(): void {
    const from = weekStart(this.selectedDate());
    if (from === this.overviewFrom()) return;
    this.overviewFrom.set(from);
    this.loadOverview();
  }

  selectOverviewDay(date: string): void {
    this.navigateToDate(date);
  }

  private loadMembers(): void {
    this.service.getHouseMembers().subscribe({
      next: (members) => this.members.set(members),
      error: () => this.members.set([]),
    });
  }

  private loadOptions(): void {
    this.isLoadingOptions.set(true);
    let recipesLoaded = false;
    let ingredientsLoaded = false;
    let preparedMealsLoaded = false;
    const checkDone = () => {
      if (recipesLoaded && ingredientsLoaded && preparedMealsLoaded) this.isLoadingOptions.set(false);
    };

    this.recipeService.list(1, 100).subscribe({
      next: (res) => { this.recipes.set(res.items); recipesLoaded = true; checkDone(); },
      error: () => { recipesLoaded = true; checkDone(); },
    });

    this.ingredientService.getAll().subscribe({
      next: (items) => { this.ingredients.set(items); ingredientsLoaded = true; checkDone(); },
      error: () => { ingredientsLoaded = true; checkDone(); },
    });
    this.preparedMealService.list().subscribe({
      next: (items) => { this.preparedMeals.set(items); preparedMealsLoaded = true; checkDone(); },
      error: () => { preparedMealsLoaded = true; checkDone(); },
    });
  }

  loadPlan(): void {
    const date = this.selectedDate();
    if (!date) return;
    const requestId = ++this.planRequestId;
    const cacheKey = mealPlannerCacheKey(this.auth?.currentUser() ?? null);
    const cachedPlan = readCachedPlan(cacheKey, date);
    const hasCachedPlan = cachedPlan !== null;

    this.isLoadingPlan.set(!hasCachedPlan);
    this.planError.set(null);
    this.plan.set(cachedPlan);
    if (cachedPlan) this.loadRecipeDetails(cachedPlan);

    this.service.getForDate(date)
      .pipe(finalize(() => {
        if (requestId === this.planRequestId && !hasCachedPlan) this.isLoadingPlan.set(false);
      }))
      .subscribe({
        next: (plan) => {
          if (requestId !== this.planRequestId || this.selectedDate() !== date) return;
          this.applyFreshPlan(plan);
          cachePlan(cacheKey, date, plan);
        },
        error: (err) => {
          if (requestId === this.planRequestId) {
            this.planError.set(getApiError(err, this.translations.translate('meal.loadError')));
          }
        },
      });
  }

  onDateChange(): void {
    const date = this.selectedDate();
    if (isValidDateString(date)) this.navigateToDate(date);
    else this.selectedDate.set(this.lastValidDate);
  }

  /** After a copy, show the target day and the week around it. */
  onPlanCopied(target: string): void {
    this.navigateToDate(target);
    this.overviewFrom.set(weekStart(target));
    this.loadOverview();
  }

  navigateToDate(date: string, replaceUrl = false): void {
    if (!isValidDateString(date)) return;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { date },
      queryParamsHandling: 'merge',
      replaceUrl,
    });
  }

  private applyDate(date: string): void {
    this.selectedDate.set(date);
    this.lastValidDate = date;
    this.openedSlots.set([]);
    this.centerOverviewOnSelected();
    this.loadPlan();
  }

  goToPreviousDay(): void {
    this.navigateToDate(addDays(this.selectedDate(), -1));
  }

  goToNextDay(): void {
    this.navigateToDate(addDays(this.selectedDate(), 1));
  }

  goToToday(): void {
    this.navigateToDate(todayString());
  }

  itemsForSlot(slot: MealSlot): MealPlanItem[] {
    return this.plan()?.meals[slot] ?? [];
  }

  /** True while an optional slot has nothing planned and the user has not opened it on this day. */
  isSlotCollapsed(slot: MealSlot): boolean {
    return this.optionalSlots.includes(slot)
      && !this.openedSlots().includes(slot)
      && !this.itemsForSlot(slot).length
      && !this.composer()[slot];
  }

  /** Swaps a collapsed optional slot for the full card so the user can add to it. */
  openSlot(slot: MealSlot): void {
    if (this.openedSlots().includes(slot)) return;
    this.openedSlots.update((slots) => [...slots, slot]);
  }

  addRecipe(slot: MealSlot): void {
    const recipeId = this.selectedRecipeId()[slot];
    if (!recipeId || this.addingSlot()) return;
    this.addingSlot.set(`recipe-${slot}`);
    this.service.addItem({ date: this.selectedDate(), slot, recipeId }).pipe(
      finalize(() => this.addingSlot.set(null))
    ).subscribe({
      next: (item) => {
        this.plan.update((p) => p ? this.appendItem(p, slot, item) : p);
        this.selectedRecipeId.update((s) => ({ ...s, [slot]: '' }));
        this.composer.update((value) => ({ ...value, [slot]: null }));
        if (item.recipeId) this.loadRecipeDetail(item.recipeId);
        this.loadOverview();
      },
      error: (err) => this.planError.set(getApiError(err, this.translations.translate('meal.addRecipeError'))),
    });
  }

  addIngredient(slot: MealSlot): void {
    const ingredientId = this.selectedIngredientId()[slot];
    if (!ingredientId || this.addingSlot()) return;
    this.addingSlot.set(`ingredient-${slot}`);
    this.service.addItem({ date: this.selectedDate(), slot, ingredientId }).pipe(
      finalize(() => this.addingSlot.set(null))
    ).subscribe({
      next: (item) => {
        this.plan.update((p) => p ? this.appendItem(p, slot, item) : p);
        this.selectedIngredientId.update((s) => ({ ...s, [slot]: '' }));
        this.composer.update((value) => ({ ...value, [slot]: null }));
        this.loadOverview();
      },
      error: (err) => this.planError.set(getApiError(err, this.translations.translate('meal.addIngredientError'))),
    });
  }

  addPreparedMeal(slot: MealSlot): void {
    const preparedMealId = this.selectedPreparedMealId()[slot];
    const meal = this.preparedMeals().find(value => value.id === preparedMealId);
    if (!meal || this.addingSlot()) return;
    this.addingSlot.set(`prepared-meal-${slot}`);
    const selections = this.preparedAddonSelections();
    const addons = meal.addons.filter(addon => addon.isRequired || selections[`${slot}-${addon.id}`]?.selected)
      .map(addon => ({ ingredientId: addon.ingredientId, quantity: selections[`${slot}-${addon.id}`]?.quantity ?? addon.quantity, unit: addon.unit }));
    this.service.addItem({ date: this.selectedDate(), slot, preparedMealId, addons }).pipe(finalize(() => this.addingSlot.set(null))).subscribe({
      next: () => { this.selectedPreparedMealId.update(value => ({ ...value, [slot]: '' })); this.composer.update(value => ({ ...value, [slot]: null })); this.loadPlan(); this.loadOverview(); },
      error: (err) => this.planError.set(getApiError(err, this.translations.translate('preparedMeals.saveError'))),
    });
  }

  /** Drop onto a slot within the current day: move the meal to that slot. */
  dropOnSlot(event: CdkDragDrop<MealSlot>, targetSlot: MealSlot): void {
    if (event.previousContainer === event.container) return;
    this.moveItem(event.item.data as MealPlanItem, this.selectedDate(), targetSlot);
  }

  /** Drop onto a day in the week grid: move the meal to that day, keeping its slot. */
  dropOnDay({ item, date }: MealDroppedOnDay): void {
    this.moveItem(item, date, item.slot);
  }

  /**
   * Relocates a meal to a new day and/or slot. The UI is updated optimistically so the
   * drop feels instant; if the backend save fails the previous plan is restored so no
   * data is lost from the view.
   */
  private moveItem(item: MealPlanItem, date: string, slot: MealSlot): void {
    if (this.movingId()) return;
    const snapshotDate = this.selectedDate();
    const sameDay = date === snapshotDate;
    if (sameDay && slot === item.slot) return;

    const snapshot = this.plan();
    this.movingId.set(item.id);
    this.planError.set(null);

    this.plan.update((p) => {
      if (!p) return p;
      const withoutItem = this.filterItem(p, item.id);
      if (!sameDay) return withoutItem;
      return this.appendItem(withoutItem, slot, { ...item, slot, sortOrder: withoutItem.meals[slot].length });
    });

    this.service.moveItem(item.id, date, slot).pipe(
      finalize(() => this.movingId.set(null))
    ).subscribe({
      next: (updated) => {
        // Only reconcile the view if the user is still looking at the day we moved from.
        if (sameDay && this.selectedDate() === date) this.plan.update((p) => p ? this.replaceItem(p, updated) : p);
        this.loadOverview();
      },
      error: (err) => {
        // Guard against restoring a stale snapshot over a day the user has since navigated to.
        if (this.selectedDate() === snapshotDate) this.plan.set(snapshot);
        this.planError.set(getApiError(err, this.translations.translate('meal.moveError')));
      },
    });
  }

  removeItem(item: MealPlanItem): void {
    if (this.removingId()) return;
    this.removingId.set(item.id);
    this.service.removeItem(item.id).pipe(
      finalize(() => this.removingId.set(null))
    ).subscribe({
      next: () => {
        this.plan.update((p) => p ? this.filterItem(p, item.id) : p);
        this.loadOverview();
      },
      error: (err) => this.planError.set(getApiError(err, this.translations.translate('meal.removeError'))),
    });
  }

  isAdding(prefix: string, slot: MealSlot): boolean {
    return this.addingSlot() === `${prefix}-${slot}`;
  }

  updateParticipants(item: MealPlanItem, userIds: string[]): void {
    if (this.busyItemId()) return;
    this.busyItemId.set(item.id);
    this.service.setParticipants(item.id, userIds).pipe(
      finalize(() => this.busyItemId.set(null))
    ).subscribe({
      next: (updated) => this.plan.update((p) => p ? this.replaceItem(p, updated) : p),
      error: (err) => this.planError.set(getApiError(err, this.translations.translate('meal.peopleError'))),
    });
  }

  saveParticipantPortion(item: MealPlanItem, { userId, portionPercent }: ParticipantPortionChange): void {
    if (this.busyItemId()) return;
    this.busyItemId.set(item.id);
    this.service.setParticipantPortion(item.id, userId, portionPercent).pipe(
      finalize(() => this.busyItemId.set(null))
    ).subscribe({
      next: (updated) => this.plan.update((p) => p ? this.replaceItem(p, updated) : p),
      error: (err) => this.planError.set(getApiError(err, this.translations.translate('meal.portionError'))),
    });
  }

  setGuests(item: MealPlanItem, guests: number): void {
    if (this.busyItemId()) return;
    this.busyItemId.set(item.id);
    this.service.setGuests(item.id, guests).pipe(
      finalize(() => this.busyItemId.set(null))
    ).subscribe({
      next: (updated) => this.plan.update((p) => p ? this.replaceItem(p, updated) : p),
      error: (err) => this.planError.set(getApiError(err, this.translations.translate('meal.guestsError'))),
    });
  }

  setSelectedRecipeId(slot: MealSlot, value: string): void {
    this.selectedRecipeId.update((s) => ({ ...s, [slot]: value }));
    if (value) this.addRecipe(slot);
  }

  setSelectedIngredientId(slot: MealSlot, value: string): void {
    this.selectedIngredientId.update((s) => ({ ...s, [slot]: value }));
    if (value) this.addIngredient(slot);
  }

  setSelectedPreparedMealId(slot: MealSlot, value: string): void {
    this.selectedPreparedMealId.update(s => ({ ...s, [slot]: value }));
    const meal = this.preparedMeals().find(candidate => candidate.id === value);
    if (meal) this.preparedAddonSelections.update(selections => ({ ...selections, ...Object.fromEntries(meal.addons.map(addon => [`${slot}-${addon.id}`, { selected: addon.isSelectedByDefault || addon.isRequired, quantity: addon.quantity }])) }));
  }

  preparedMealForSlot(slot: MealSlot): PreparedMeal | undefined { return this.preparedMeals().find(meal => meal.id === this.selectedPreparedMealId()[slot]); }
  setPreparedAddonSelected(slot: MealSlot, addonId: string, selected: boolean): void { this.preparedAddonSelections.update(value => ({ ...value, [`${slot}-${addonId}`]: { ...(value[`${slot}-${addonId}`] ?? { quantity: 1 }), selected } })); }
  setPreparedAddonQuantity(slot: MealSlot, addonId: string, quantity: number): void { this.preparedAddonSelections.update(value => ({ ...value, [`${slot}-${addonId}`]: { ...(value[`${slot}-${addonId}`] ?? { selected: true }), quantity } })); }

  openComposer(slot: MealSlot, type: MealPlanItemType): void {
    this.composer.update((value) => ({ ...value, [slot]: type }));
  }

  closeComposer(slot: MealSlot): void {
    this.selectedRecipeId.update((value) => ({ ...value, [slot]: '' }));
    this.selectedIngredientId.update((value) => ({ ...value, [slot]: '' }));
    this.selectedPreparedMealId.update((value) => ({ ...value, [slot]: '' }));
    this.composer.update((value) => ({ ...value, [slot]: null }));
  }

  itemCost(item: MealPlanItem): number | null {
    const singleServingPrice = this.pricePerServing(item);
    return singleServingPrice === null ? null : singleServingPrice * item.servings;
  }

  pricePerServing(item: MealPlanItem): number | null {
    if (item.type === 'ingredient') {
      const ingredient = this.ingredients().find((candidate) => candidate.id === item.ingredientId);
      if (!ingredient) return null;
      return ingredient.pricePer100BaseUnits * directIngredientQuantity(ingredient, 1) / 100;
    }

    const detail = item.recipeId ? this.recipeDetails()[item.recipeId] : undefined;
    return detail ? recipePricePerServing(detail, this.ingredients()) : null;
  }

  caloriesPerServing(item: MealPlanItem): number | null {
    return mealCaloriesPerServing(item, this.recipeDetails(), this.ingredients(), this.preparedMeals());
  }

  addToShoppingList(item: MealPlanItem): void {
    if (this.shoppingItemState()[item.id] === 'adding') return;
    this.shoppingItemState.update((state) => ({ ...state, [item.id]: 'adding' }));
    this.planError.set(null);

    const needsRecipe = item.type === 'recipe' && item.recipeId && !this.recipeDetails()[item.recipeId];

    const ready: Observable<RecipeDetail | null> = needsRecipe
      ? this.recipeService.get(item.recipeId!)
      : of(null);

    ready.pipe(
      switchMap((detail) => {
        if (detail) this.cacheRecipeDetail(detail);
        return this.shoppingListIntegration.addToShoppingList(
          item,
          this.recipeDetails(),
          this.ingredients(),
          this.translations.translate('meal.noCatalogueIngredients')
        );
      }),
      finalize(() => {
        if (this.shoppingItemState()[item.id] === 'adding') {
          this.shoppingItemState.update((state) => {
            const next = { ...state };
            delete next[item.id];
            return next;
          });
        }
      })
    ).subscribe({
      next: () => this.shoppingItemState.update((state) => ({ ...state, [item.id]: 'added' })),
      error: (error: unknown) => this.planError.set(getApiError(error, this.translations.translate('meal.shoppingError'))),
    });
  }

  private allItems(): MealPlanItem[] {
    return this.slots.flatMap((slot) => this.itemsForSlot(slot));
  }

  private loadRecipeDetails(plan: DailyMealPlan): void {
    const ids = [...new Set(this.slots.flatMap((slot) => plan.meals[slot])
      .map((item) => item.recipeId)
      .filter((id): id is string => !!id))];
    for (const id of ids) this.loadRecipeDetail(id);
  }

  private loadRecipeDetail(id: string): void {
    if (this.recipeDetails()[id]) return;
    this.recipeService.get(id).pipe(catchError(() => of(null))).subscribe((detail) => {
      if (detail) this.cacheRecipeDetail(detail);
    });
  }

  private cacheRecipeDetail(detail: RecipeDetail): void {
    this.recipeDetails.update((details) => ({ ...details, [detail.id]: detail }));
  }

  private applyFreshPlan(plan: DailyMealPlan): void {
    const current = this.plan();
    if (current && JSON.stringify(current) === JSON.stringify(plan)) return;
    this.plan.set(plan);
    this.loadRecipeDetails(plan);
  }

  private appendItem(plan: DailyMealPlan, slot: MealSlot, item: MealPlanItem): DailyMealPlan {
    return {
      ...plan,
      meals: { ...plan.meals, [slot]: [...plan.meals[slot], item] },
    };
  }

  private replaceItem(plan: DailyMealPlan, updated: MealPlanItem): DailyMealPlan {
    const meals = { ...plan.meals };
    meals[updated.slot] = meals[updated.slot].map((i) => (i.id === updated.id ? updated : i));
    return { ...plan, meals };
  }

  private filterItem(plan: DailyMealPlan, id: string): DailyMealPlan {
    const meals = { ...plan.meals };
    for (const slot of this.slots) {
      meals[slot] = meals[slot].filter((i) => i.id !== id);
    }
    return { ...plan, meals };
  }
}
