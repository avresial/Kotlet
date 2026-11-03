import { CdkDragDrop, CdkDropList } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { MealPlanItem, MealPlanOverviewDay, MealSlot } from '../../models/meal-planner.models';
import { addDays, parseLocalDate } from '../../meal-plan-dates';

export interface MealDroppedOnDay {
  item: MealPlanItem;
  date: string;
}

/** Week-at-a-glance grid of planned slots. Days are drop targets for moving a meal to another day. */
@Component({
  selector: 'app-meal-plan-overview',
  imports: [TranslatePipe, CdkDropList],
  templateUrl: './meal-plan-overview.html',
  styleUrl: './meal-plan-overview.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealPlanOverview {
  private readonly translations = inject(TranslationService);

  readonly days = input.required<MealPlanOverviewDay[]>();
  readonly from = input.required<string>();
  readonly dayCount = input.required<number>();
  readonly slots = input.required<MealSlot[]>();
  readonly slotLabels = input.required<Record<MealSlot, string>>();
  readonly selectedDate = input.required<string>();

  readonly daySelected = output<string>();
  readonly mealDropped = output<MealDroppedOnDay>();

  readonly label = computed(() => {
    const from = this.from();
    const to = addDays(from, this.dayCount() - 1);
    return `${this.dayNumber(from)} – ${this.dayNumber(to)}`;
  });

  isPlanned(day: MealPlanOverviewDay, slot: MealSlot): boolean {
    return day.plannedSlots.includes(slot);
  }

  mealNames(day: MealPlanOverviewDay, slot: MealSlot): string[] {
    return day.plannedMeals?.[slot] ?? [];
  }

  dayName(date: string): string {
    return new Intl.DateTimeFormat(this.translations.language(), { weekday: 'short' }).format(parseLocalDate(date));
  }

  dayNumber(date: string): string {
    return new Intl.DateTimeFormat(this.translations.language(), { day: 'numeric', month: 'short' }).format(parseLocalDate(date));
  }

  dropOnDay(event: CdkDragDrop<string>, date: string): void {
    this.mealDropped.emit({ item: event.item.data as MealPlanItem, date });
  }
}
