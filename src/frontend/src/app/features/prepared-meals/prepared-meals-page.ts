import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { PreparedMeal, PreparedMealImage } from './prepared-meal.models';
import { PreparedMealService } from './prepared-meal.service';
import { Ingredient } from '../ingredients/ingredient.models';
import { IngredientService } from '../ingredients/ingredient.service';

@Component({
  selector: 'app-prepared-meals-page',
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './prepared-meals-page.html',
  styleUrl: './prepared-meals-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PreparedMealsPage implements OnInit, OnDestroy {
  private readonly service = inject(PreparedMealService);
  private readonly ingredientService = inject(IngredientService);
  readonly meals = signal<PreparedMeal[]>([]);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly addons = signal<{ ingredientId: string; quantity: number; unit: string; isSelectedByDefault: boolean; isRequired: boolean; sortOrder: number }[]>([]);
  readonly images = signal<PreparedMealImage[]>([]);
  readonly imageUrls = signal<Record<string, string>>({});
  readonly saving = signal(false);
  readonly error = signal('');
  readonly activeMeals = computed(() => this.meals().filter(meal => !meal.isArchived).length);
  id: string | null = null; name = ''; brand = ''; servings = 1; price: number | null = null; instructions = ''; imageFile: File | null = null; imageAltText = '';
  ngOnInit() { this.load(); this.ingredientService.getAll().subscribe(value => this.ingredients.set(value)); }
  ngOnDestroy() { this.clearImageUrls(); }
  load() { this.error.set(''); this.service.list(true).subscribe({ next: value => this.meals.set(value), error: () => this.error.set('preparedMeals.loadError') }); }
  save() { const request = { name: this.name, brand: this.brand || null, servings: this.servings, price: this.price, preparationInstructions: this.instructions || null, addons: this.addons() }; this.error.set(''); this.saving.set(true); (this.id ? this.service.update(this.id, request) : this.service.create(request)).pipe(finalize(() => this.saving.set(false))).subscribe({ next: () => { this.reset(); this.load(); }, error: () => this.error.set('preparedMeals.saveError') }); }
  edit(meal: PreparedMeal) { this.id = meal.id; this.name = meal.name; this.brand = meal.brand ?? ''; this.servings = meal.servings; this.price = meal.price ?? null; this.instructions = meal.preparationInstructions ?? ''; this.addons.set(meal.addons.map(({ ingredientId, quantity, unit, isSelectedByDefault, isRequired, sortOrder }) => ({ ingredientId, quantity, unit, isSelectedByDefault, isRequired, sortOrder }))); this.loadImages(); window.scrollTo({ top: 0, behavior: 'smooth' }); }
  toggleArchive(meal: PreparedMeal) { (meal.isArchived ? this.service.restore(meal.id) : this.service.archive(meal.id)).subscribe(() => this.load()); }
  addAddon() { this.addons.update(value => [...value, { ingredientId: '', quantity: 1, unit: 'g', isSelectedByDefault: false, isRequired: false, sortOrder: value.length }]); }
  removeAddon(index: number) { this.addons.update(value => value.filter((_, i) => i !== index).map((addon, sortOrder) => ({ ...addon, sortOrder }))); }
  updateAddon(index: number, field: string, value: unknown) { this.addons.update(addons => addons.map((addon, i) => i === index ? { ...addon, [field]: value } : addon)); }
  imageUrl(image: PreparedMealImage) { return this.imageUrls()[image.id] ?? ''; }
  loadImages() { if (this.id) this.service.listImages(this.id).subscribe(images => { this.clearImageUrls(); this.images.set(images); for (const image of images) this.service.getImageContent(image).subscribe(blob => this.imageUrls.update(urls => ({ ...urls, [image.id]: URL.createObjectURL(blob) }))); }); }
  selectImage(event: Event) { this.imageFile = (event.target as HTMLInputElement).files?.[0] ?? null; }
  uploadImage() { if (this.id && this.imageFile) this.service.uploadImage(this.id, this.imageFile, this.imageAltText).subscribe(() => { this.imageFile = null; this.imageAltText = ''; this.loadImages(); }); }
  saveAlt(image: PreparedMealImage, event: Event) { if (this.id) this.service.updateImage(this.id, image.id, (event.target as HTMLInputElement).value).subscribe(() => this.loadImages()); }
  deleteImage(image: PreparedMealImage) { if (this.id) this.service.deleteImage(this.id, image.id).subscribe(() => this.loadImages()); }
  moveImage(index: number, delta: number) { if (!this.id) return; const images = [...this.images()]; [images[index], images[index + delta]] = [images[index + delta], images[index]]; this.service.reorderImages(this.id, images.map(image => image.id)).subscribe(() => this.images.set(images)); }
  reset() { this.id = null; this.name = this.brand = this.instructions = ''; this.servings = 1; this.price = null; this.addons.set([]); this.images.set([]); this.clearImageUrls(); this.error.set(''); }
  hasInvalidAddons() { return this.addons().some(addon => !addon.ingredientId || addon.quantity <= 0 || !addon.unit.trim()); }
  private clearImageUrls() { Object.values(this.imageUrls()).forEach(URL.revokeObjectURL); this.imageUrls.set({}); }
}
