import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { RecipeSummary } from '../../../recipes/models/recipe.models';
import { RecipeService } from '../../../recipes/services/recipe.service';
import { DailyMealPlan, MealPlanItem, MealSlot } from '../../models/meal-planner.models';
import { MealPlannerService } from '../../services/meal-planner.service';

@Component({
  selector: 'app-meal-planner-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './meal-planner-page.html',
  styleUrl: './meal-planner-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealPlannerPage implements OnInit {
  private readonly service = inject(MealPlannerService);
  private readonly recipeService = inject(RecipeService);
  private readonly ingredientService = inject(IngredientService);

  readonly slots: MealSlot[] = ['breakfast', 'dinner', 'supper'];
  readonly slotLabels: Record<MealSlot, string> = {
    breakfast: 'Breakfast',
    dinner: 'Dinner',
    supper: 'Supper',
  };

  readonly selectedDate = signal(this.todayString());
  readonly plan = signal<DailyMealPlan | null>(null);
  readonly isLoadingPlan = signal(false);
  readonly planError = signal<string | null>(null);

  readonly recipes = signal<RecipeSummary[]>([]);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly isLoadingOptions = signal(true);

  readonly selectedRecipeId = signal<Record<MealSlot, string>>({ breakfast: '', dinner: '', supper: '' });
  readonly selectedIngredientId = signal<Record<MealSlot, string>>({ breakfast: '', dinner: '', supper: '' });
  readonly addingSlot = signal<string | null>(null);
  readonly removingId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadOptions();
    this.loadPlan();
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
        next: (plan) => this.plan.set(plan),
        error: (err) => this.planError.set(getApiError(err, 'Unable to load meal plan.')),
      });
  }

  onDateChange(): void {
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
      },
      error: (err) => this.planError.set(getApiError(err, 'Unable to remove item.')),
    });
  }

  isAdding(prefix: string, slot: MealSlot): boolean {
    return this.addingSlot() === `${prefix}-${slot}`;
  }

  private appendItem(plan: DailyMealPlan, slot: MealSlot, item: MealPlanItem): DailyMealPlan {
    return {
      ...plan,
      meals: { ...plan.meals, [slot]: [...plan.meals[slot], item] },
    };
  }

  private filterItem(plan: DailyMealPlan, id: string): DailyMealPlan {
    const meals = { ...plan.meals };
    for (const slot of this.slots) {
      meals[slot] = meals[slot].filter((i) => i.id !== id);
    }
    return { ...plan, meals };
  }

  private todayString(): string {
    return new Date().toISOString().slice(0, 10);
  }
}
