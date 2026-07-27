import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { AuthService } from '../../../../core/auth/auth.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import {
  isPortionPercentInRange,
  MAX_PORTION_PERCENT,
  MIN_PORTION_PERCENT,
  normalizePortionPercent,
  recipeCaloriesPerServing,
} from '../../../meal-planner/meal-planner-calculations';
import { HouseMember, MealParticipant, MealPlanItem } from '../../../meal-planner/models/meal-planner.models';
import { MealPlannerService } from '../../../meal-planner/services/meal-planner.service';
import { ShoppingListIntegrationService } from '../../../meal-planner/services/shopping-list-integration.service';
import { RecipeDetail } from '../../models/recipe.models';

/** A person eating this recipe, with an editable portion size, held only while the panel is open. */
interface RecipeParticipant extends MealParticipant {
  displayName: string;
}

/**
 * Lets a signed-in user add a recipe's ingredients to their shopping list, choosing who is eating
 * (house members and/or anonymous guests) and adjusting each person's portion — mirroring the
 * portion controls of the meal planner. The chosen portions are summed into a serving count that
 * scales the recipe's ingredient quantities before they are merged onto the shopping list.
 */
@Component({
  selector: 'app-recipe-shopping-list',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './recipe-shopping-list.html',
  styleUrl: './recipe-shopping-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Lets the host section (e.g. the Ingredients header row) give the expanded panel a full-width line.
  host: { '[class.is-open]': 'isOpen()' },
})
export class RecipeShoppingList implements OnInit {
  readonly recipe = input.required<RecipeDetail>();
  readonly ingredients = input.required<readonly Ingredient[]>();

  private readonly mealPlannerService = inject(MealPlannerService);
  private readonly shoppingListIntegration = inject(ShoppingListIntegrationService);
  private readonly translations = inject(TranslationService);
  private readonly auth = inject(AuthService);

  readonly isOpen = signal(false);
  readonly members = signal<HouseMember[]>([]);
  readonly participants = signal<RecipeParticipant[]>([]);
  readonly guests = signal(0);
  readonly state = signal<'idle' | 'adding' | 'added'>('idle');
  readonly error = signal<string | null>(null);
  /** Inline validation messages for out-of-range portion inputs, keyed by participant/field. */
  readonly fieldErrors = signal<Record<string, string>>({});

  readonly caloriesPerServing = computed(() => recipeCaloriesPerServing(this.recipe(), this.ingredients()));
  /** Total servings requested across everyone eating; drives the scaled shopping quantities. */
  readonly totalServings = computed(() =>
    this.participants().reduce((total, person) => total + person.portionPercent / 100, 0) + this.guests()
  );
  readonly availableMembers = computed(() => {
    const assigned = new Set(this.participants().map((p) => p.userId));
    return this.members().filter((m) => !assigned.has(m.userId));
  });
  readonly isHouseFull = computed(() => this.members().length > 0 && this.availableMembers().length === 0);

  ngOnInit(): void {
    this.mealPlannerService.getHouseMembers().subscribe({
      next: (members) => this.members.set(members),
      error: () => this.members.set([]),
    });
  }

  open(): void {
    this.isOpen.set(true);
    this.error.set(null);
    if (this.state() === 'added') this.state.set('idle');
    if (!this.participants().length && !this.guests()) this.seedCurrentUser();
  }

  close(): void {
    this.isOpen.set(false);
  }

  /** Adds the signed-in user as the first person to eat, so the panel opens ready to confirm. */
  private seedCurrentUser(): void {
    const user = this.auth.currentUser();
    if (!user) return;
    const name = user.displayName?.trim() || this.translations.translate('meal.user');
    this.participants.set([{ userId: user.id, displayName: name, isCurrentUser: true, portionPercent: 100 }]);
  }

  addParticipant(userId: string): void {
    const member = this.members().find((m) => m.userId === userId);
    if (!member || this.participants().some((p) => p.userId === userId)) return;
    const isCurrentUser = this.auth.currentUser()?.id === member.userId;
    this.participants.update((people) => [
      ...people,
      { userId: member.userId, displayName: member.displayName, isCurrentUser, portionPercent: 100 },
    ]);
    this.state.set('idle');
  }

  removeParticipant(userId: string): void {
    this.participants.update((people) => people.filter((p) => p.userId !== userId));
    this.clearParticipantErrors(userId);
    this.state.set('idle');
  }

  addHouse(): void {
    if (this.isHouseFull() || !this.members().length) return;
    const currentUserId = this.auth.currentUser()?.id;
    this.participants.set(this.members().map((member) => ({
      userId: member.userId,
      displayName: member.displayName,
      isCurrentUser: member.userId === currentUserId,
      portionPercent: 100,
    })));
    this.state.set('idle');
  }

  clearParticipants(): void {
    this.participants.set([]);
    this.fieldErrors.set({});
    this.state.set('idle');
  }

  addGuest(): void {
    if (this.guests() >= 99) return;
    this.guests.update((value) => value + 1);
    this.state.set('idle');
  }

  removeGuest(): void {
    if (this.guests() <= 0) return;
    this.guests.update((value) => value - 1);
    this.state.set('idle');
  }

  guestSeats(): number[] {
    return Array.from({ length: this.guests() }, (_, index) => index);
  }

  participantCalories(person: RecipeParticipant): number {
    return this.caloriesPerServing() * person.portionPercent / 100;
  }

  /**
   * Validates a portion input as the user types, surfacing an inline message the moment the value
   * falls outside the allowed range. Persisting the value is left to the commit handler.
   */
  validateField(person: RecipeParticipant, field: 'calories' | 'portion', rawValue: string): void {
    const key = this.fieldKey(person.userId, field);
    if (rawValue.trim() === '') { this.clearFieldError(key); return; }

    const percent = this.fieldToPercent(field, Number(rawValue));
    if (percent !== null && isPortionPercentInRange(percent)) {
      this.clearFieldError(key);
    } else {
      this.fieldErrors.update((errors) => ({ ...errors, [key]: this.rangeMessage(field) }));
    }
  }

  /**
   * Commits a portion input on blur/change: the value is clamped into the allowed range, the field is
   * reset to the clamped display value so the view can never show an out-of-range number, and the
   * participant's portion is updated.
   */
  commitField(person: RecipeParticipant, field: 'calories' | 'portion', input: HTMLInputElement): void {
    this.clearFieldError(this.fieldKey(person.userId, field));

    const percent = this.fieldToPercent(field, Number(input.value));
    if (input.value.trim() === '' || percent === null || Number.isNaN(percent)) {
      input.value = this.fieldDisplay(field, person.portionPercent);
      return;
    }

    const portionPercent = normalizePortionPercent(percent);
    input.value = this.fieldDisplay(field, portionPercent);
    this.setPortion(person.userId, portionPercent);
  }

  fieldError(person: RecipeParticipant, field: 'calories' | 'portion'): string | null {
    return this.fieldErrors()[this.fieldKey(person.userId, field)] ?? null;
  }

  /** Distinct validation messages currently active, rendered together beneath the portion table. */
  activeErrors(): string[] {
    return [...new Set(Object.values(this.fieldErrors()))];
  }

  addToShoppingList(): void {
    if (this.state() === 'adding') return;
    if (this.totalServings() <= 0) {
      this.error.set(this.translations.translate('recipes.shoppingNoPeople'));
      return;
    }
    this.state.set('adding');
    this.error.set(null);

    const recipe = this.recipe();
    const item: MealPlanItem = {
      id: recipe.id,
      slot: 'dinner',
      type: 'recipe',
      recipeId: recipe.id,
      displayName: recipe.title,
      sortOrder: 0,
      participants: this.participants(),
      guests: this.guests(),
      servings: this.totalServings(),
      servingsOverridden: false,
    };

    this.shoppingListIntegration.addToShoppingList(
      item,
      { [recipe.id]: recipe },
      this.ingredients(),
      this.translations.translate('meal.noCatalogueIngredients'),
    ).pipe(
      finalize(() => { if (this.state() === 'adding') this.state.set('idle'); })
    ).subscribe({
      next: () => this.state.set('added'),
      error: (err: unknown) => this.error.set(getApiError(err, this.translations.translate('meal.shoppingError'))),
    });
  }

  private setPortion(userId: string, portionPercent: number): void {
    this.participants.update((people) =>
      people.map((p) => (p.userId === userId ? { ...p, portionPercent } : p)));
    this.state.set('idle');
  }

  private fieldToPercent(field: 'calories' | 'portion', value: number): number | null {
    if (Number.isNaN(value)) return null;
    if (field === 'portion') return value;
    const caloriesPerServing = this.caloriesPerServing();
    return caloriesPerServing ? value / caloriesPerServing * 100 : null;
  }

  private fieldDisplay(field: 'calories' | 'portion', portionPercent: number): string {
    return field === 'portion'
      ? String(portionPercent)
      : (this.caloriesPerServing() * portionPercent / 100).toFixed(0);
  }

  private rangeMessage(field: 'calories' | 'portion'): string {
    const caloriesPerServing = this.caloriesPerServing();
    const bound = (percent: number) => field === 'portion'
      ? String(percent)
      : String(Math.round(caloriesPerServing * percent / 100));
    return this.translations.translate('meal.rangeError')
      .replace('{min}', bound(MIN_PORTION_PERCENT))
      .replace('{max}', bound(MAX_PORTION_PERCENT))
      .replace('{unit}', field === 'calories' ? 'kcal' : '%');
  }

  private fieldKey(userId: string, field: 'calories' | 'portion'): string {
    return `${userId}:${field}`;
  }

  private clearFieldError(key: string): void {
    if (!(key in this.fieldErrors())) return;
    this.fieldErrors.update((errors) => {
      const next = { ...errors };
      delete next[key];
      return next;
    });
  }

  private clearParticipantErrors(userId: string): void {
    this.fieldErrors.update((errors) =>
      Object.fromEntries(Object.entries(errors).filter(([key]) => !key.startsWith(`${userId}:`))));
  }
}
