import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, Observable, of, switchMap, tap } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { IngredientPicker } from '../../../ingredients/components/ingredient-picker/ingredient-picker';
import { RecipeDetail, RecipeSummary } from '../../../recipes/models/recipe.models';
import { RecipeService } from '../../../recipes/services/recipe.service';
import { RecipePicker } from '../../../recipes/components/recipe-picker/recipe-picker';
import { ShoppingListService } from '../../../shopping-list/shopping-list.service';
import { DailyMealPlan, HouseMember, MealPlanItem, MealPlanItemType, MealPlanOverviewDay, MealSlot } from '../../models/meal-planner.models';
import { MealPlannerService } from '../../services/meal-planner.service';
import {
  directIngredientCaloriesPerServing,
  directIngredientQuantity,
  recipeCaloriesPerServing,
  recipePricePerServing,
  scaleRecipeQuantity,
} from '../../meal-planner-calculations';

interface PersonCalories {
  id: string;
  name: string;
  calories: number;
}

@Component({
  selector: 'app-meal-planner-page',
  imports: [FormsModule, RouterLink, IngredientPicker, RecipePicker],
  templateUrl: './meal-planner-page.html',
  styleUrl: './meal-planner-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealPlannerPage implements OnInit {
  private readonly service = inject(MealPlannerService);
  private readonly recipeService = inject(RecipeService);
  private readonly ingredientService = inject(IngredientService);
  private readonly shoppingListService = inject(ShoppingListService);

  readonly slots: MealSlot[] = ['breakfast', 'dinner', 'supper'];
  readonly slotLabels: Record<MealSlot, string> = {
    breakfast: 'Breakfast',
    dinner: 'Lunch',
    supper: 'Dinner',
  };

  private readonly overviewDays = 28;
  readonly selectedDate = signal(this.todayString());
  readonly overviewFrom = signal(this.todayString());
  readonly overview = signal<MealPlanOverviewDay[]>([]);
  readonly overviewLabel = computed(() => {
    const from = this.overviewFrom();
    if (from === this.todayString()) return `Next ${this.overviewDays} days`;
    const to = this.addDays(from, this.overviewDays - 1);
    return `${this.dayNumber(from)} – ${this.dayNumber(to)}`;
  });
  readonly plan = signal<DailyMealPlan | null>(null);
  readonly isLoadingPlan = signal(false);
  readonly planError = signal<string | null>(null);

  readonly recipes = signal<RecipeSummary[]>([]);
  readonly recipeDetails = signal<Record<string, RecipeDetail>>({});
  readonly ingredients = signal<Ingredient[]>([]);
  readonly isLoadingOptions = signal(true);

  readonly members = signal<HouseMember[]>([]);

  readonly selectedRecipeId = signal<Record<MealSlot, string>>({ breakfast: '', dinner: '', supper: '' });
  readonly selectedIngredientId = signal<Record<MealSlot, string>>({ breakfast: '', dinner: '', supper: '' });
  readonly addingSlot = signal<string | null>(null);
  readonly removingId = signal<string | null>(null);
  readonly busyItemId = signal<string | null>(null);
  readonly composer = signal<Record<MealSlot, MealPlanItemType | null>>({ breakfast: null, dinner: null, supper: null });
  readonly shoppingItemState = signal<Record<string, 'adding' | 'added'>>({});

  readonly dayTotal = computed(() => this.allItems().reduce((total, item) => total + (this.itemCost(item) ?? 0), 0));
  readonly dayServings = computed(() => this.allItems().reduce((total, item) => total + item.servings, 0));
  readonly dayCalories = computed(() => this.allItems().reduce(
    (total, item) => total + (this.caloriesPerServing(item) ?? 0) * item.servings,
    0,
  ));
  readonly caloriesByPerson = computed<PersonCalories[]>(() => {
    const totals = new Map<string, PersonCalories>();
    let guestCalories = 0;
    let unassignedCalories = 0;

    for (const item of this.allItems()) {
      const caloriesPerServing = this.caloriesPerServing(item);
      if (caloriesPerServing === null || item.servings === 0) continue;
      const headcount = item.participants.length + item.guests;
      if (headcount === 0) {
        unassignedCalories += caloriesPerServing * item.servings;
        continue;
      }

      const caloriesPerPerson = caloriesPerServing * item.servings / headcount;
      for (const participant of item.participants) {
        const existing = totals.get(participant.userId);
        totals.set(participant.userId, {
          id: participant.userId,
          name: participant.displayName,
          calories: (existing?.calories ?? 0) + caloriesPerPerson,
        });
      }
      guestCalories += caloriesPerPerson * item.guests;
    }

    const result = [...totals.values()].sort((a, b) => b.calories - a.calories || a.name.localeCompare(b.name));
    if (guestCalories > 0) result.push({ id: 'guests', name: 'Guests', calories: guestCalories });
    if (unassignedCalories > 0) result.push({ id: 'unassigned', name: 'Unassigned servings', calories: unassignedCalories });
    return result;
  });

  ngOnInit(): void {
    this.loadOptions();
    this.loadMembers();
    this.loadOverview();
    this.loadPlan();
  }

  loadOverview(): void {
    this.service.getOverview(this.overviewFrom(), this.overviewDays).subscribe({
      next: (days) => this.overview.set(days),
      error: () => this.overview.set([]),
    });
  }

  /** Re-centers the overview window on the selected date when it falls outside the current range. */
  private centerOverviewOnSelected(): void {
    const date = this.selectedDate();
    const from = this.overviewFrom();
    const to = this.addDays(from, this.overviewDays - 1);
    if (date >= from && date <= to) return;
    this.overviewFrom.set(this.addDays(date, -Math.floor(this.overviewDays / 2)));
    this.loadOverview();
  }

  selectOverviewDay(date: string): void {
    this.selectedDate.set(date);
    this.loadPlan();
  }

  isPlanned(day: MealPlanOverviewDay, slot: MealSlot): boolean {
    return day.plannedSlots.includes(slot);
  }

  dayName(date: string): string {
    return new Intl.DateTimeFormat(undefined, { weekday: 'short' }).format(this.localDate(date));
  }

  dayNumber(date: string): string {
    return new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short' }).format(this.localDate(date));
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
    const checkDone = () => {
      if (recipesLoaded && ingredientsLoaded) this.isLoadingOptions.set(false);
    };

    this.recipeService.list(1, 100).subscribe({
      next: (res) => { this.recipes.set(res.items); recipesLoaded = true; checkDone(); },
      error: () => { recipesLoaded = true; checkDone(); },
    });

    this.ingredientService.getAll().subscribe({
      next: (items) => { this.ingredients.set(items); ingredientsLoaded = true; checkDone(); },
      error: () => { ingredientsLoaded = true; checkDone(); },
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
        error: (err) => this.planError.set(getApiError(err, 'Unable to load meal plan.')),
      });
  }

  onDateChange(): void {
    this.centerOverviewOnSelected();
    this.loadPlan();
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
      error: (err) => this.planError.set(getApiError(err, 'Unable to add recipe.')),
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
      error: (err) => this.planError.set(getApiError(err, 'Unable to add ingredient.')),
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
      error: (err) => this.planError.set(getApiError(err, 'Unable to remove item.')),
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
      error: (err) => this.planError.set(getApiError(err, 'Unable to update the people for this meal.')),
    });
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
      error: (err) => this.planError.set(getApiError(err, 'Unable to update guests.')),
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

  openComposer(slot: MealSlot, type: MealPlanItemType): void {
    this.composer.update((value) => ({ ...value, [slot]: type }));
  }

  closeComposer(slot: MealSlot): void {
    this.selectedRecipeId.update((value) => ({ ...value, [slot]: '' }));
    this.selectedIngredientId.update((value) => ({ ...value, [slot]: '' }));
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

    const ready: Observable<RecipeDetail | null> = item.type === 'recipe' && item.recipeId && !this.recipeDetails()[item.recipeId]
      ? this.recipeService.get(item.recipeId).pipe(tap((detail) => this.cacheRecipeDetail(detail)))
      : of(null);

    ready.pipe(
      switchMap(() => {
        const quantities = this.shoppingQuantities(item);
        if (!quantities.length) throw new Error('No catalogue ingredients were found for this meal.');
        return this.shoppingListService.getAll().pipe(
          switchMap((current) => forkJoin(quantities.map(({ ingredient, quantity }) => {
            const existing = current.find((entry) => entry.ingredientId === ingredient.id);
            return existing
              ? this.shoppingListService.update(existing, { quantity: existing.quantity + quantity, isPurchased: false })
              : this.shoppingListService.create(ingredient.id, quantity);
          })))
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
      error: (error: unknown) => this.planError.set(getApiError(error, error instanceof Error ? error.message : 'Unable to add this meal to the shopping list.')),
    });
  }

  private shoppingQuantities(item: MealPlanItem): { ingredient: Ingredient; quantity: number }[] {
    if (item.type === 'ingredient') {
      const ingredient = this.ingredients().find((candidate) => candidate.id === item.ingredientId);
      if (!ingredient) return [];
      const quantity = directIngredientQuantity(ingredient, item.servings);
      return quantity > 0 ? [{ ingredient, quantity }] : [];
    }

    const detail = item.recipeId ? this.recipeDetails()[item.recipeId] : undefined;
    const totals = new Map<string, { ingredient: Ingredient; quantity: number }>();
    for (const recipeIngredient of detail?.ingredients ?? []) {
      const ingredient = this.ingredients().find((candidate) => candidate.id === recipeIngredient.ingredientId);
      if (!ingredient) continue;
      const existing = totals.get(ingredient.id);
      const quantity = scaleRecipeQuantity(
        recipeIngredient.normalizedQuantity,
        detail!.servings,
        item.servings,
      );
      totals.set(ingredient.id, { ingredient, quantity: (existing?.quantity ?? 0) + quantity });
    }
    return [...totals.values()];
  }

  private findIngredient(name: string): Ingredient | undefined {
    const normalized = name.trim().toLocaleLowerCase();
    return this.ingredients().find((ingredient) => ingredient.name.trim().toLocaleLowerCase() === normalized);
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
