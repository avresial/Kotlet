import { ChangeDetectionStrategy, Component, computed, forwardRef, input, output, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { RecipeSummary } from '../../models/recipe.models';

@Component({
  selector: 'app-recipe-picker',
  templateUrl: './recipe-picker.html',
  styleUrl: './recipe-picker.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => RecipePicker),
    multi: true,
  }],
})
export class RecipePicker implements ControlValueAccessor {
  readonly recipes = input.required<readonly RecipeSummary[]>();
  readonly placeholder = input('Start typing a recipe…');
  readonly ariaLabel = input('Recipe');
  readonly recipeSelected = output<RecipeSummary>();
  readonly query = signal('');
  readonly isOpen = signal(false);
  readonly activeIndex = signal(0);
  readonly formDisabled = signal(false);

  readonly suggestions = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    if (!query) return [];
    return this.recipes()
      .filter(recipe => recipe.title.toLocaleLowerCase().includes(query))
      .sort((a, b) => {
        const aStarts = a.title.toLocaleLowerCase().startsWith(query);
        const bStarts = b.title.toLocaleLowerCase().startsWith(query);
        return Number(bStarts) - Number(aStarts) || a.title.localeCompare(b.title);
      })
      .slice(0, 8);
  });

  private value = '';
  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  writeValue(value: string | null): void {
    this.value = value ?? '';
    this.query.set(this.recipes().find(recipe => recipe.id === this.value)?.title ?? '');
  }

  registerOnChange(fn: (value: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(disabled: boolean): void { this.formDisabled.set(disabled); }

  onInput(value: string): void {
    this.query.set(value);
    this.activeIndex.set(0);
    this.isOpen.set(value.trim().length > 0);
    if (this.value) {
      this.value = '';
      this.onChange('');
    }
  }

  select(recipe: RecipeSummary): void {
    this.value = recipe.id;
    this.query.set(recipe.title);
    this.isOpen.set(false);
    this.onChange(this.value);
    this.onTouched();
    this.recipeSelected.emit(recipe);
  }

  onBlur(): void {
    this.isOpen.set(false);
    this.onTouched();
    if (!this.value) this.query.set('');
  }

  onKeydown(event: KeyboardEvent): void {
    const suggestions = this.suggestions();
    if (event.key === 'Escape') { this.isOpen.set(false); return; }
    if (!suggestions.length) return;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.isOpen.set(true);
      this.activeIndex.update(index => Math.min(index + 1, suggestions.length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeIndex.update(index => Math.max(index - 1, 0));
    } else if (event.key === 'Enter' && this.isOpen()) {
      event.preventDefault();
      this.select(suggestions[this.activeIndex()]);
    }
  }
}
