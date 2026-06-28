import { ChangeDetectionStrategy, Component, computed, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Ingredient } from '../../ingredient.models';

@Component({
  selector: 'app-ingredient-picker',
  templateUrl: './ingredient-picker.html',
  styleUrl: './ingredient-picker.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => IngredientPicker),
    multi: true,
  }],
})
export class IngredientPicker implements ControlValueAccessor {
  readonly ingredients = input.required<readonly Ingredient[]>();
  readonly placeholder = input('Start typing an ingredient…');
  readonly ariaLabel = input('Ingredient');
  readonly query = signal('');
  readonly isOpen = signal(false);
  readonly activeIndex = signal(0);
  readonly formDisabled = signal(false);

  readonly suggestions = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    if (!query) return [];
    return this.ingredients()
      .filter(ingredient => ingredient.name.toLocaleLowerCase().includes(query))
      .sort((a, b) => {
        const aStarts = a.name.toLocaleLowerCase().startsWith(query);
        const bStarts = b.name.toLocaleLowerCase().startsWith(query);
        return Number(bStarts) - Number(aStarts) || a.name.localeCompare(b.name);
      })
      .slice(0, 8);
  });

  private value = '';
  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  writeValue(value: string | null): void {
    this.value = value ?? '';
    this.query.set(this.ingredients().find(ingredient => ingredient.id === this.value)?.name ?? '');
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

  select(ingredient: Ingredient): void {
    this.value = ingredient.id;
    this.query.set(ingredient.name);
    this.isOpen.set(false);
    this.onChange(ingredient.id);
    this.onTouched();
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
