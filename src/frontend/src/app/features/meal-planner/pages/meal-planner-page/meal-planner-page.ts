import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, finalize, Observable, of, switchMap } from 'rxjs';
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
import { DailyMealPlan, HouseMember, MealParticipant, MealPlanItem, MealPlanItemType, MealPlanOverviewDay, MealSlot } from '../../models/meal-planner.models';
import { MealPlannerService } from '../../services/meal-planner.service';
import { ShoppingListIntegrationService } from '../../services/shopping-list-integration.service';
import {
  allocateCaloriesByPerson,
  directIngredientCaloriesPerServing,
  directIngredientQuantity,
  isPortionPercentInRange,
  MAX_PORTION_PERCENT,
  MIN_PORTION_PERCENT,
  normalizePortionPercent,
  recipeCaloriesPerServing,
  recipePricePerServing,
  scaleRecipeQuantity,
} from '../../meal-planner-calculations';

/** The three editable numeric columns of the portion table, all of which drive a participant's portion percentage. */
type ParticipantField = 'calories' | 'quantity' | 'portion';

export function weekStart(date: string): string {
  const value = new Date(`${date}T00:00:00`);
  value.setDate(value.getDate() - (value.getDay() + 6) % 7);
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
}

export function isValidDateString(value: string | null): value is string {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [year, month, day] = value.split('-').map(Number);
  const date = new Date(year, month - 1, day);
  return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day;
}

@Component({
  selector: 'app-meal-planner-page',
  imports: [FormsModule, RouterLink, IngredientPicker, RecipePicker, TranslatePipe, CdkDropListGroup, CdkDropList, CdkDrag, CdkDragHandle],
  templateUrl: './meal-planner-page.html',
  styleUrl: './meal-planner-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealPlannerPage implements OnInit {
  readonly weekStart = weekStart;
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
    : this.todayString();
  private lastValidDate = this.initialDate;

  readonly slots: MealSlot[] = ['breakfast', 'second-breakfast', 'dinner', 'snack', 'supper'];
  readonly slotLabels = computed<Record<MealSlot, string>>(() => ({
    breakfast: this.translations.translate('meal.breakfast'),
    'second-breakfast': this.translations.translate('meal.secondBreakfast'),
    dinner: this.translations.translate('meal.lunch'),
    snack: this.translations.translate('meal.snack'),
    supper: this.translations.translate('meal.dinner'),
  }));

  private readonly overviewDays = 7;
  readonly selectedDate = signal(this.initialDate);
  readonly overviewFrom = signal(weekStart(this.initialDate));
  readonly overview = signal<MealPlanOverviewDay[]>([]);
  readonly selectedDayHasMeals = computed(() =>
    !!this.overview().find((day) => day.date === this.selectedDate())?.plannedSlots.length
  );
  readonly overviewLabel = computed(() => {
    const from = this.overviewFrom();
    const to = this.addDays(from, this.overviewDays - 1);
    return `${this.dayNumber(from)} – ${this.dayNumber(to)}`;
  });
  readonly plan = signal<DailyMealPlan | null>(null);
  readonly isLoadingPlan = signal(false);
  readonly planError = signal<string | null>(null);
  readonly copyTargetDate = signal(this.addDays(this.initialDate, 1));
  readonly isCopying = signal(false);
  readonly copyWeekTargetDate = signal(this.addDays(weekStart(this.initialDate), 7));

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
  /** Inline validation messages for out-of-range portion inputs, keyed by item/participant/field. */
  readonly fieldErrors = signal<Record<string, string>>({});

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

  isPlanned(day: MealPlanOverviewDay, slot: MealSlot): boolean {
    return day.plannedSlots.includes(slot);
  }

  dayName(date: string): string {
    return new Intl.DateTimeFormat(this.translations.language(), { weekday: 'short' }).format(this.localDate(date));
  }

  dayNumber(date: string): string {
    return new Intl.DateTimeFormat(this.translations.language(), { day: 'numeric', month: 'short' }).format(this.localDate(date));
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
    this.isLoadingPlan.set(true);
    this.planError.set(null);
    this.service.getForDate(date)
      .pipe(finalize(() => this.isLoadingPlan.set(false)))
      .subscribe({
        next: (plan) => {
          this.plan.set(plan);
          this.loadRecipeDetails(plan);
        },
        error: (err) => this.planError.set(getApiError(err, this.translations.translate('meal.loadError'))),
      });
  }

  onDateChange(): void {
    const date = this.selectedDate();
    if (isValidDateString(date)) this.navigateToDate(date);
    else this.selectedDate.set(this.lastValidDate);
  }

  copyDay(): void {
    const target = this.copyTargetDate();
    if (!target || target === this.selectedDate() || this.isCopying()) return;
    this.isCopying.set(true); this.planError.set(null);
    this.service.copyDay(this.selectedDate(), target).pipe(finalize(() => this.isCopying.set(false))).subscribe({
      next: () => {
        this.navigateToDate(target);
        this.overviewFrom.set(weekStart(target)); this.loadOverview();
      },
      error: (error) => this.planError.set(getApiError(error, this.translations.translate('meal.copyDayError'))),
    });
  }

  copyWeek(): void {
    const source = weekStart(this.selectedDate());
    const target = weekStart(this.copyWeekTargetDate());
    if (source === target || this.isCopying()) return;
    this.isCopying.set(true); this.planError.set(null);
    this.service.copyWeek(source, target).pipe(finalize(() => this.isCopying.set(false))).subscribe({
      next: () => {
        this.navigateToDate(target);
        this.overviewFrom.set(target); this.loadOverview();
      },
      error: (error) => this.planError.set(getApiError(error, this.translations.translate('meal.copyWeekError'))),
    });
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
    this.updateCopyTargets();
    this.centerOverviewOnSelected();
    this.loadPlan();
  }

  private updateCopyTargets(): void {
    const selected = this.selectedDate();
    this.copyTargetDate.set(this.addDays(selected, 1));
    this.copyWeekTargetDate.set(this.addDays(weekStart(selected), 7));
  }

  goToPreviousDay(): void {
    this.navigateToDate(this.addDays(this.selectedDate(), -1));
  }

  goToNextDay(): void {
    this.navigateToDate(this.addDays(this.selectedDate(), 1));
  }

  goToToday(): void {
    this.navigateToDate(this.todayString());
  }

  itemsForSlot(slot: MealSlot): MealPlanItem[] {
    return this.plan()?.meals[slot] ?? [];
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
  dropOnDay(event: CdkDragDrop<string>, targetDate: string): void {
    const item = event.item.data as MealPlanItem;
    this.moveItem(item, targetDate, item.slot);
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

  /** House members not yet assigned to the given meal, for the "add a person" picker. */
  availableMembers(item: MealPlanItem): HouseMember[] {
    const assigned = new Set(item.participants.map((p) => p.userId));
    return this.members().filter((m) => !assigned.has(m.userId));
  }

  /** True when every house member is already assigned to the meal. */
  isHouseFull(item: MealPlanItem): boolean {
    return this.members().length > 0 && this.availableMembers(item).length === 0;
  }

  addParticipant(item: MealPlanItem, userId: string): void {
    if (!userId || this.busyItemId()) return;
    this.updateParticipants(item, [...item.participants.map((p) => p.userId), userId]);
  }

  removeParticipant(item: MealPlanItem, userId: string): void {
    if (this.busyItemId()) return;
    this.updateParticipants(item, item.participants.map((p) => p.userId).filter((id) => id !== userId));
  }

  addHouse(item: MealPlanItem): void {
    if (this.busyItemId() || this.isHouseFull(item)) return;
    this.updateParticipants(item, this.members().map((m) => m.userId));
  }

  clearParticipants(item: MealPlanItem): void {
    if (this.busyItemId() || item.participants.length === 0) return;
    this.updateParticipants(item, []);
  }

  private updateParticipants(item: MealPlanItem, userIds: string[]): void {
    this.busyItemId.set(item.id);
    this.service.setParticipants(item.id, userIds).pipe(
      finalize(() => this.busyItemId.set(null))
    ).subscribe({
      next: (updated) => this.plan.update((p) => p ? this.replaceItem(p, updated) : p),
      error: (err) => this.planError.set(getApiError(err, this.translations.translate('meal.peopleError'))),
    });
  }

  /**
   * Validates a portion input as the user types, surfacing an inline message the moment the value
   * falls outside the allowed range. Persisting is left to the commit handler on blur/change.
   */
  validateParticipantField(item: MealPlanItem, participant: MealParticipant, field: ParticipantField, rawValue: string): void {
    const key = this.participantFieldKey(item, participant.userId, field);
    if (rawValue.trim() === '') { this.clearFieldError(key); return; }

    const range = this.participantFieldRange(item, field);
    const percent = this.participantFieldToPercent(item, field, Number(rawValue));
    if (percent !== null && isPortionPercentInRange(percent)) {
      this.clearFieldError(key);
    } else {
      this.fieldErrors.update((errors) => ({ ...errors, [key]: this.rangeMessage(item, field, range) }));
    }
  }

  /**
   * Commits a portion input on blur/change: the value is clamped into the allowed range, the field is
   * reset to the clamped display value so the view can never show an out-of-range number, and the
   * result is persisted. This is the single source of truth for the three interlinked columns.
   */
  commitParticipantField(item: MealPlanItem, participant: MealParticipant, field: ParticipantField, input: HTMLInputElement): void {
    this.clearFieldError(this.participantFieldKey(item, participant.userId, field));

    const percent = this.participantFieldToPercent(item, field, Number(input.value));
    if (input.value.trim() === '' || percent === null || Number.isNaN(percent)) {
      // Empty or unusable input: revert the field to the participant's current portion.
      input.value = this.participantFieldDisplay(item, field, participant.portionPercent);
      return;
    }

    const portionPercent = normalizePortionPercent(percent);
    input.value = this.participantFieldDisplay(item, field, portionPercent);
    this.saveParticipantPortion(item, participant, portionPercent);
  }

  fieldError(item: MealPlanItem, participant: MealParticipant, field: ParticipantField): string | null {
    return this.fieldErrors()[this.participantFieldKey(item, participant.userId, field)] ?? null;
  }

  /** Distinct validation messages currently active for a meal, rendered together beneath its portion table. */
  itemFieldErrors(item: MealPlanItem): string[] {
    const prefix = `${item.id}:`;
    const messages = Object.entries(this.fieldErrors())
      .filter(([key]) => key.startsWith(prefix))
      .map(([, message]) => message);
    return [...new Set(messages)];
  }

  private saveParticipantPortion(item: MealPlanItem, participant: MealParticipant, portionPercent: number): void {
    if (this.busyItemId() || portionPercent === participant.portionPercent) return;
    this.busyItemId.set(item.id);
    this.service.setParticipantPortion(item.id, participant.userId, portionPercent).pipe(
      finalize(() => this.busyItemId.set(null))
    ).subscribe({
      next: (updated) => this.plan.update((p) => p ? this.replaceItem(p, updated) : p),
      error: (err) => this.planError.set(getApiError(err, this.translations.translate('meal.portionError'))),
    });
  }

  /** Converts a field's entered value into the portion percentage it represents, or null when it can't be resolved. */
  private participantFieldToPercent(item: MealPlanItem, field: ParticipantField, value: number): number | null {
    if (Number.isNaN(value)) return null;
    switch (field) {
      case 'portion':
        return value;
      case 'calories': {
        const caloriesPerServing = this.caloriesPerServing(item);
        return caloriesPerServing ? value / caloriesPerServing * 100 : null;
      }
      case 'quantity': {
        const ingredient = this.ingredientFor(item);
        if (!ingredient) return null;
        const regularQuantity = ingredient.isCountable ? 1 : directIngredientQuantity(ingredient, 1);
        return regularQuantity ? value / regularQuantity * 100 : null;
      }
    }
  }

  /** Renders the display value a field should show for a given portion percentage. */
  private participantFieldDisplay(item: MealPlanItem, field: ParticipantField, portionPercent: number): string {
    switch (field) {
      case 'portion':
        return String(portionPercent);
      case 'calories':
        return ((this.caloriesPerServing(item) ?? 0) * portionPercent / 100).toFixed(0);
      case 'quantity':
        return String(this.participantQuantityForPercent(item, portionPercent));
    }
  }

  /** The valid input range for a field, expressed in that field's own units. */
  private participantFieldRange(item: MealPlanItem, field: ParticipantField): { min: number; max: number } {
    switch (field) {
      case 'portion':
        return { min: MIN_PORTION_PERCENT, max: MAX_PORTION_PERCENT };
      case 'calories': {
        const caloriesPerServing = this.caloriesPerServing(item) ?? 0;
        return { min: caloriesPerServing * MIN_PORTION_PERCENT / 100, max: caloriesPerServing * MAX_PORTION_PERCENT / 100 };
      }
      case 'quantity':
        return {
          min: this.participantQuantityForPercent(item, MIN_PORTION_PERCENT),
          max: this.participantQuantityForPercent(item, MAX_PORTION_PERCENT),
        };
    }
  }

  private rangeMessage(item: MealPlanItem, field: ParticipantField, range: { min: number; max: number }): string {
    const unit = field === 'calories' ? 'kcal'
      : field === 'portion' ? '%' : this.participantQuantityUnit(item);
    return this.translations.translate('meal.rangeError')
      .replace('{min}', this.formatRangeBound(field, range.min))
      .replace('{max}', this.formatRangeBound(field, range.max))
      .replace('{unit}', unit);
  }

  private formatRangeBound(field: ParticipantField, value: number): string {
    return field === 'quantity' ? String(Math.round(value * 100) / 100) : String(Math.round(value));
  }

  private participantFieldKey(item: MealPlanItem, userId: string, field: ParticipantField): string {
    return `${item.id}:${userId}:${field}`;
  }

  private clearFieldError(key: string): void {
    if (!(key in this.fieldErrors())) return;
    this.fieldErrors.update((errors) => {
      const next = { ...errors };
      delete next[key];
      return next;
    });
  }

  participantCalories(item: MealPlanItem, participant: MealParticipant): number {
    return (this.caloriesPerServing(item) ?? 0) * participant.portionPercent / 100;
  }

  participantQuantity(item: MealPlanItem, participant: MealParticipant): number {
    return this.participantQuantityForPercent(item, participant.portionPercent);
  }

  participantQuantityForPercent(item: MealPlanItem, portionPercent: number): number {
    const ingredient = this.ingredientFor(item);
    if (!ingredient) return 0;
    return ingredient.isCountable
      ? portionPercent / 100
      : directIngredientQuantity(ingredient, portionPercent / 100);
  }

  participantQuantityUnit(item: MealPlanItem): string {
    const ingredient = this.ingredientFor(item);
    return ingredient?.isCountable ? this.translations.translate('units.piece') : ingredient?.measurementUnit ?? '';
  }

  private ingredientFor(item: MealPlanItem): Ingredient | undefined {
    return this.ingredients().find((candidate) => candidate.id === item.ingredientId);
  }

  /** Builds an index array used to render one chip per guest. */
  guestSeats(item: MealPlanItem): number[] {
    return Array.from({ length: item.guests }, (_, index) => index);
  }

  addGuest(item: MealPlanItem): void {
    if (this.busyItemId() || item.guests >= 99) return;
    this.setGuests(item, item.guests + 1);
  }

  removeGuest(item: MealPlanItem): void {
    if (this.busyItemId() || item.guests <= 0) return;
    this.setGuests(item, item.guests - 1);
  }

  private setGuests(item: MealPlanItem, guests: number): void {
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
    if (item.type === 'ingredient') {
      const ingredient = this.ingredients().find((candidate) => candidate.id === item.ingredientId);
      return ingredient ? directIngredientCaloriesPerServing(ingredient) : null;
    }

    const detail = item.recipeId ? this.recipeDetails()[item.recipeId] : undefined;
    return detail ? recipeCaloriesPerServing(detail, this.ingredients()) : null;
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

  private todayString(): string {
    return this.dateString(new Date());
  }

  /** Returns the date string offset from the given ISO date by the supplied number of days. */
  private addDays(value: string, days: number): string {
    const date = this.localDate(value);
    date.setDate(date.getDate() + days);
    return this.dateString(date);
  }

  private dateString(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private localDate(value: string): Date {
    const [year, month, day] = value.split('-').map(Number);
    return new Date(year, month - 1, day);
  }
}
