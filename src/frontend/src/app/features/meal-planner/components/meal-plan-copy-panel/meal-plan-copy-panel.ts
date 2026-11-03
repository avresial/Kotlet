import { ChangeDetectionStrategy, Component, effect, ElementRef, HostListener, inject, input, linkedSignal, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { MealPlannerService } from '../../services/meal-planner.service';
import { addDays, weekStart } from '../../meal-plan-dates';

/**
 * Popover that copies the selected day or its week onto another date. Copying is a rare,
 * destructive-feeling action, so it lives behind a trigger instead of in the day toolbar.
 */
@Component({
  selector: 'app-meal-plan-copy-panel',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './meal-plan-copy-panel.html',
  styleUrl: './meal-plan-copy-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealPlanCopyPanel {
  private readonly service = inject(MealPlannerService);
  private readonly translations = inject(TranslationService);

  readonly weekStart = weekStart;
  readonly selectedDate = input.required<string>();
  /** A day can only be copied once its plan has loaded. */
  readonly hasPlan = input.required<boolean>();
  /** Emits the date the copy landed on, so the page can show it. */
  readonly copied = output<string>();

  readonly copyTargetDate = linkedSignal(() => addDays(this.selectedDate(), 1));
  readonly copyWeekTargetDate = linkedSignal(() => addDays(weekStart(this.selectedDate()), 7));
  readonly isCopying = signal(false);
  readonly isOpen = signal(false);
  /** Copy failures are reported inside the popover so the message stays next to the controls that caused it. */
  readonly copyError = signal<string | null>(null);
  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('copyTrigger');
  private readonly copyDayInput = viewChild<ElementRef<HTMLInputElement>>('copyDayInput');

  constructor() {
    // Move focus into the popover as soon as it is rendered.
    effect(() => this.copyDayInput()?.nativeElement.focus());
  }

  toggle(event: MouseEvent): void {
    event.stopPropagation();
    this.copyError.set(null);
    this.isOpen.update((isOpen) => !isOpen);
  }

  /** Closes the popover, returning focus to its trigger when the user dismissed it deliberately. */
  close(restoreFocus = false): void {
    if (!this.isOpen()) return;
    this.isOpen.set(false);
    this.copyError.set(null);
    if (restoreFocus) this.trigger()?.nativeElement.focus();
  }

  @HostListener('document:click')
  closeFromDocument(): void {
    this.close();
  }

  @HostListener('document:keydown.escape')
  closeFromEscape(): void {
    this.close(true);
  }

  copyDay(): void {
    const target = this.copyTargetDate();
    if (!target || target === this.selectedDate() || this.isCopying()) return;
    this.runCopy(this.service.copyDay(this.selectedDate(), target), target, 'meal.copyDayError');
  }

  copyWeek(): void {
    const source = weekStart(this.selectedDate());
    const target = weekStart(this.copyWeekTargetDate());
    if (source === target || this.isCopying()) return;
    this.runCopy(this.service.copyWeek(source, target), target, 'meal.copyWeekError');
  }

  private runCopy(request: Observable<unknown>, target: string, errorKey: string): void {
    this.isCopying.set(true);
    this.copyError.set(null);
    request.pipe(finalize(() => this.isCopying.set(false))).subscribe({
      next: () => {
        this.close();
        this.copied.emit(target);
      },
      error: (error) => this.copyError.set(getApiError(error, this.translations.translate(errorKey))),
    });
  }
}
