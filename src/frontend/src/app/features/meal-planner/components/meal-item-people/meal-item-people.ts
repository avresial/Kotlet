import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { HouseMember, MealParticipant, MealPlanItem } from '../../models/meal-planner.models';
import {
  directIngredientQuantity,
  isPortionPercentInRange,
  MAX_PORTION_PERCENT,
  MIN_PORTION_PERCENT,
  normalizePortionPercent,
} from '../../meal-planner-calculations';

/** The three editable numeric columns of the portion table, all of which drive a participant's portion percentage. */
export type ParticipantField = 'calories' | 'quantity' | 'portion';

export interface ParticipantPortionChange {
  userId: string;
  portionPercent: number;
}

const maxGuests = 99;

/**
 * Edits who eats one planned meal: the per-person portion table, house members and guests.
 * It only emits the requested changes; the page persists them and feeds the updated item back.
 */
@Component({
  selector: 'app-meal-item-people',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './meal-item-people.html',
  styleUrl: './meal-item-people.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealItemPeople {
  private readonly translations = inject(TranslationService);

  readonly item = input.required<MealPlanItem>();
  readonly members = input.required<HouseMember[]>();
  readonly ingredients = input.required<Ingredient[]>();
  readonly caloriesPerServing = input.required<number | null>();
  /** True while a change to this meal is being saved. */
  readonly isBusy = input(false);

  readonly participantsChange = output<string[]>();
  readonly portionChange = output<ParticipantPortionChange>();
  readonly guestsChange = output<number>();

  readonly maxGuests = maxGuests;
  /** Inline validation messages for out-of-range portion inputs, keyed by participant/field. */
  readonly fieldErrors = signal<Record<string, string>>({});

  /** House members not yet assigned to the meal, for the "add a person" picker. */
  readonly availableMembers = computed(() => {
    const assigned = new Set(this.item().participants.map((participant) => participant.userId));
    return this.members().filter((member) => !assigned.has(member.userId));
  });
  /** True when every house member is already assigned to the meal. */
  readonly isHouseFull = computed(() => this.members().length > 0 && this.availableMembers().length === 0);
  /** Distinct validation messages currently active, rendered together beneath the portion table. */
  readonly itemFieldErrors = computed(() => [...new Set(Object.values(this.fieldErrors()))]);
  /** Index array used to render one chip per guest. */
  readonly guestSeats = computed(() => Array.from({ length: this.item().guests }, (_, index) => index));

  private readonly ingredient = computed(() =>
    this.ingredients().find((candidate) => candidate.id === this.item().ingredientId));

  addParticipant(userId: string): void {
    if (!userId) return;
    this.participantsChange.emit([...this.participantIds(), userId]);
  }

  removeParticipant(userId: string): void {
    this.participantsChange.emit(this.participantIds().filter((id) => id !== userId));
  }

  addHouse(): void {
    if (this.isHouseFull()) return;
    this.participantsChange.emit(this.members().map((member) => member.userId));
  }

  clearParticipants(): void {
    if (this.item().participants.length === 0) return;
    this.participantsChange.emit([]);
  }

  addGuest(): void {
    if (this.item().guests >= maxGuests) return;
    this.guestsChange.emit(this.item().guests + 1);
  }

  removeGuest(): void {
    if (this.item().guests <= 0) return;
    this.guestsChange.emit(this.item().guests - 1);
  }

  /**
   * Validates a portion input as the user types, surfacing an inline message the moment the value
   * falls outside the allowed range. Persisting is left to the commit handler on blur/change.
   */
  validateParticipantField(participant: MealParticipant, field: ParticipantField, rawValue: string): void {
    const key = this.fieldKey(participant.userId, field);
    if (rawValue.trim() === '') { this.clearFieldError(key); return; }

    const percent = this.fieldToPercent(field, Number(rawValue));
    if (percent !== null && isPortionPercentInRange(percent)) {
      this.clearFieldError(key);
    } else {
      this.fieldErrors.update((errors) => ({ ...errors, [key]: this.rangeMessage(field, this.fieldRange(field)) }));
    }
  }

  /**
   * Commits a portion input on blur/change: the value is clamped into the allowed range, the field is
   * reset to the clamped display value so the view can never show an out-of-range number, and the
   * result is emitted. This is the single source of truth for the three interlinked columns.
   */
  commitParticipantField(participant: MealParticipant, field: ParticipantField, input: HTMLInputElement): void {
    this.clearFieldError(this.fieldKey(participant.userId, field));

    const percent = this.fieldToPercent(field, Number(input.value));
    if (input.value.trim() === '' || percent === null || Number.isNaN(percent)) {
      // Empty or unusable input: revert the field to the participant's current portion.
      input.value = this.fieldDisplay(field, participant.portionPercent);
      return;
    }

    const portionPercent = normalizePortionPercent(percent);
    input.value = this.fieldDisplay(field, portionPercent);
    if (portionPercent === participant.portionPercent) return;
    this.portionChange.emit({ userId: participant.userId, portionPercent });
  }

  fieldError(participant: MealParticipant, field: ParticipantField): string | null {
    return this.fieldErrors()[this.fieldKey(participant.userId, field)] ?? null;
  }

  participantCalories(participant: MealParticipant): number {
    return (this.caloriesPerServing() ?? 0) * participant.portionPercent / 100;
  }

  participantQuantity(participant: MealParticipant): number {
    return this.quantityForPercent(participant.portionPercent);
  }

  quantityForPercent(portionPercent: number): number {
    const ingredient = this.ingredient();
    if (!ingredient) return 0;
    return ingredient.isCountable
      ? portionPercent / 100
      : directIngredientQuantity(ingredient, portionPercent / 100);
  }

  quantityUnit(): string {
    const ingredient = this.ingredient();
    return ingredient?.isCountable ? this.translations.translate('units.piece') : ingredient?.measurementUnit ?? '';
  }

  private participantIds(): string[] {
    return this.item().participants.map((participant) => participant.userId);
  }

  /** Converts a field's entered value into the portion percentage it represents, or null when it can't be resolved. */
  private fieldToPercent(field: ParticipantField, value: number): number | null {
    if (Number.isNaN(value)) return null;
    switch (field) {
      case 'portion':
        return value;
      case 'calories': {
        const caloriesPerServing = this.caloriesPerServing();
        return caloriesPerServing ? value / caloriesPerServing * 100 : null;
      }
      case 'quantity': {
        const ingredient = this.ingredient();
        if (!ingredient) return null;
        const regularQuantity = ingredient.isCountable ? 1 : directIngredientQuantity(ingredient, 1);
        return regularQuantity ? value / regularQuantity * 100 : null;
      }
    }
  }

  /** Renders the display value a field should show for a given portion percentage. */
  private fieldDisplay(field: ParticipantField, portionPercent: number): string {
    switch (field) {
      case 'portion':
        return String(portionPercent);
      case 'calories':
        return ((this.caloriesPerServing() ?? 0) * portionPercent / 100).toFixed(0);
      case 'quantity':
        return String(this.quantityForPercent(portionPercent));
    }
  }

  /** The valid input range for a field, expressed in that field's own units. */
  private fieldRange(field: ParticipantField): { min: number; max: number } {
    switch (field) {
      case 'portion':
        return { min: MIN_PORTION_PERCENT, max: MAX_PORTION_PERCENT };
      case 'calories': {
        const caloriesPerServing = this.caloriesPerServing() ?? 0;
        return { min: caloriesPerServing * MIN_PORTION_PERCENT / 100, max: caloriesPerServing * MAX_PORTION_PERCENT / 100 };
      }
      case 'quantity':
        return {
          min: this.quantityForPercent(MIN_PORTION_PERCENT),
          max: this.quantityForPercent(MAX_PORTION_PERCENT),
        };
    }
  }

  private rangeMessage(field: ParticipantField, range: { min: number; max: number }): string {
    const unit = field === 'calories' ? 'kcal'
      : field === 'portion' ? '%' : this.quantityUnit();
    return this.translations.translate('meal.rangeError')
      .replace('{min}', this.formatRangeBound(field, range.min))
      .replace('{max}', this.formatRangeBound(field, range.max))
      .replace('{unit}', unit);
  }

  private formatRangeBound(field: ParticipantField, value: number): string {
    return field === 'quantity' ? String(Math.round(value * 100) / 100) : String(Math.round(value));
  }

  private fieldKey(userId: string, field: ParticipantField): string {
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
}
