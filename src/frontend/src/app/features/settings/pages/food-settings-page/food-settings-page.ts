import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { finalize, forkJoin } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { Ingredient } from '../../../ingredients/ingredient.models';
import { IngredientService } from '../../../ingredients/ingredient.service';
import { FoodSettings, FoodSettingsService } from '../../food-settings.service';

type Option = { label: string; value: number };

@Component({ selector: 'app-food-settings-page', imports: [ButtonModule, FormsModule, InputTextModule, MessageModule, RouterLink, TranslatePipe], templateUrl: './food-settings-page.html', styleUrl: './food-settings-page.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class FoodSettingsPage implements OnInit {
  private readonly service = inject(FoodSettingsService);
  private readonly ingredientService = inject(IngredientService);
  readonly allergens: Option[] = ['Gluten', 'Crustaceans', 'Eggs', 'Fish', 'Peanuts', 'Soybeans', 'Milk', 'Tree nuts', 'Celery', 'Mustard', 'Sesame', 'Sulphites', 'Lupin', 'Molluscs'].map((label, i) => ({ label, value: 1 << i }));
  readonly attributes: Option[] = [{ label: 'Lactose', value: 4 }, { label: 'Alcohol', value: 8 }, { label: 'Caffeine', value: 16 }, { label: 'High histamine', value: 32 }, { label: 'High FODMAP', value: 64 }, { label: 'Spicy food', value: 512 }];
  readonly diets: Option[] = [{ label: 'No specific diet', value: 0 }, { label: 'Vegan', value: 1 }, { label: 'Vegetarian', value: 2 }, { label: 'Pescatarian', value: 4 }];
  readonly settings = signal<FoodSettings>({ avoidedAllergens: 0, avoidedAttributes: 0, requiredSuitability: 0, excludedIngredientIds: [] });
  readonly ingredients = signal<Ingredient[]>([]); readonly search = signal(''); readonly loading = signal(true); readonly saving = signal(false); readonly saved = signal(false); readonly error = signal<string | null>(null);
  readonly matches = computed(() => { const q = this.search().trim().toLowerCase(); const ids = new Set(this.settings().excludedIngredientIds); return q ? this.ingredients().filter(x => !ids.has(x.id) && x.name.toLowerCase().includes(q)).slice(0, 8) : []; });
  readonly excluded = computed(() => { const ids = new Set(this.settings().excludedIngredientIds); return this.ingredients().filter(x => ids.has(x.id)); });
  ngOnInit(): void { forkJoin({ settings: this.service.get(), ingredients: this.ingredientService.getAll() }).pipe(finalize(() => this.loading.set(false))).subscribe({ next: x => { this.settings.set(x.settings); this.ingredients.set(x.ingredients); }, error: e => this.error.set(getApiError(e, 'Unable to load food settings.')) }); }
  selected(field: 'avoidedAllergens' | 'avoidedAttributes', value: number) { return (this.settings()[field] & value) !== 0; }
  toggle(field: 'avoidedAllergens' | 'avoidedAttributes', value: number) { this.settings.update(x => ({ ...x, [field]: (x[field] & value) ? x[field] & ~value : x[field] | value })); }
  setDiet(value: number) { this.settings.update(x => ({ ...x, requiredSuitability: value })); }
  exclude(id: string) { this.settings.update(x => ({ ...x, excludedIngredientIds: [...x.excludedIngredientIds, id] })); this.search.set(''); }
  include(id: string) { this.settings.update(x => ({ ...x, excludedIngredientIds: x.excludedIngredientIds.filter(x => x !== id) })); }
  save() { this.saving.set(true); this.saved.set(false); this.error.set(null); this.service.save(this.settings()).pipe(finalize(() => this.saving.set(false))).subscribe({ next: x => { this.settings.set(x); this.saved.set(true); }, error: e => this.error.set(getApiError(e, 'Unable to save food settings.')) }); }
}
