import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { PreparedMeal, PreparedMealImage } from './prepared-meal.models';
import { PreparedMealService } from './prepared-meal.service';
import { Ingredient } from '../ingredients/ingredient.models';
import { IngredientService } from '../ingredients/ingredient.service';

@Component({
  selector: 'app-prepared-meals-page', imports: [FormsModule, TranslatePipe], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<main class="page"><header><p>{{ 'preparedMeals.eyebrow' | t }}</p><h1>{{ 'preparedMeals.title' | t }}</h1></header>
    <form (ngSubmit)="save()"><label>{{ 'preparedMeals.name' | t }}<input name="name" required maxlength="160" [(ngModel)]="name" /></label>
      <label>{{ 'preparedMeals.brand' | t }}<input name="brand" [(ngModel)]="brand" /></label>
      <label>{{ 'preparedMeals.servings' | t }}<input name="servings" type="number" min="1" required [(ngModel)]="servings" /></label>
      <label>{{ 'preparedMeals.price' | t }}<input name="price" type="number" min="0" step="0.01" [(ngModel)]="price" /></label>
      <label class="wide">{{ 'preparedMeals.instructions' | t }}<textarea name="instructions" [(ngModel)]="instructions"></textarea></label>
      <fieldset class="wide"><legend>{{ 'preparedMeals.addons' | t }}</legend>
        @for (addon of addons(); track $index) { <div><select [ngModel]="addon.ingredientId" (ngModelChange)="updateAddon($index, 'ingredientId', $event)" [name]="'ingredient-' + $index"><option value="">{{ 'common.ingredient' | t }}</option>@for (ingredient of ingredients(); track ingredient.id) { <option [value]="ingredient.id">{{ ingredient.name }}</option> }</select>
          <input type="number" min="0.01" step="0.01" [ngModel]="addon.quantity" (ngModelChange)="updateAddon($index, 'quantity', $event)" [name]="'quantity-' + $index" /><input [ngModel]="addon.unit" (ngModelChange)="updateAddon($index, 'unit', $event)" [name]="'unit-' + $index" />
          <label><input type="checkbox" [ngModel]="addon.isSelectedByDefault" (ngModelChange)="updateAddon($index, 'isSelectedByDefault', $event)" [name]="'default-' + $index" />{{ 'preparedMeals.default' | t }}</label>
          <label><input type="checkbox" [ngModel]="addon.isRequired" (ngModelChange)="updateAddon($index, 'isRequired', $event)" [name]="'required-' + $index" />{{ 'preparedMeals.required' | t }}</label><button type="button" (click)="removeAddon($index)">×</button></div> }
        <button type="button" (click)="addAddon()">{{ 'preparedMeals.addAddon' | t }}</button></fieldset>
      <button [disabled]="saving() || !name.trim() || servings < 1">{{ 'preparedMeals.save' | t }}</button><button type="button" (click)="reset()">{{ 'common.cancel' | t }}</button>
      @if (id) { <fieldset class="wide"><legend>{{ 'preparedMeals.gallery' | t }}</legend><div class="gallery">@for (image of images(); track image.id; let i = $index) { <figure><img [src]="imageUrl(image)" [alt]="image.altText || name" /><input maxlength="300" [value]="image.altText || ''" (change)="saveAlt(image, $event)" /><div><button type="button" [disabled]="i === 0" (click)="moveImage(i, -1)">↑</button><button type="button" [disabled]="i === images().length - 1" (click)="moveImage(i, 1)">↓</button><button type="button" (click)="deleteImage(image)">×</button></div></figure> }</div>
        @if (images().length < 10) { <label>{{ 'preparedMeals.image' | t }}<input type="file" accept="image/jpeg,image/png,image/webp" (change)="selectImage($event)" /></label><label>{{ 'preparedMeals.altText' | t }}<input [(ngModel)]="imageAltText" name="imageAltText" maxlength="300" /></label><button type="button" [disabled]="!imageFile" (click)="uploadImage()">{{ 'preparedMeals.upload' | t }}</button> }</fieldset> }
      </form>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    <ul>@for (meal of meals(); track meal.id) { <li><div><strong>{{ meal.name }}</strong><span>{{ meal.brand }} · {{ meal.servings }} {{ 'preparedMeals.servings' | t }}</span></div>
      <button (click)="edit(meal)">{{ 'common.edit' | t }}</button><button (click)="toggleArchive(meal)">{{ (meal.isArchived ? 'preparedMeals.restore' : 'preparedMeals.archive') | t }}</button></li> } @empty { <li>{{ 'preparedMeals.empty' | t }}</li> }</ul></main>`,
  styles: [`.page{max-width:70rem;margin:auto;padding:2rem}form{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:1rem;margin:2rem 0;padding:1.5rem;border:1px solid var(--surface-border);border-radius:1rem}label{display:grid;gap:.4rem}.wide{grid-column:1/-1}input,textarea{padding:.7rem;border:1px solid var(--surface-border);border-radius:.5rem;background:var(--surface-card);color:inherit}.gallery{display:grid;grid-template-columns:repeat(auto-fill,minmax(10rem,1fr));gap:1rem}.gallery figure{margin:0;display:grid;gap:.4rem}.gallery img{width:100%;aspect-ratio:4/3;object-fit:cover;border-radius:.5rem}ul{list-style:none;padding:0;display:grid;gap:.75rem}li{display:flex;gap:.75rem;align-items:center;padding:1rem;border:1px solid var(--surface-border);border-radius:.75rem}li div{display:grid;margin-right:auto}button{padding:.6rem .9rem}`]
})
export class PreparedMealsPage implements OnInit, OnDestroy {
  private readonly service = inject(PreparedMealService); private readonly ingredientService = inject(IngredientService); readonly meals = signal<PreparedMeal[]>([]); readonly ingredients = signal<Ingredient[]>([]); readonly addons = signal<{ ingredientId: string; quantity: number; unit: string; isSelectedByDefault: boolean; isRequired: boolean; sortOrder: number }[]>([]); readonly images = signal<PreparedMealImage[]>([]); readonly imageUrls = signal<Record<string, string>>({}); readonly saving = signal(false); readonly error = signal('');
  id: string | null = null; name = ''; brand = ''; servings = 1; price: number | null = null; instructions = ''; imageFile: File | null = null; imageAltText = '';
  ngOnInit() { this.load(); this.ingredientService.getAll().subscribe(value => this.ingredients.set(value)); }
  ngOnDestroy() { this.clearImageUrls(); }
  load() { this.service.list(true).subscribe({ next: value => this.meals.set(value), error: () => this.error.set('preparedMeals.loadError') }); }
  save() { const request = { name: this.name, brand: this.brand || null, servings: this.servings, price: this.price, preparationInstructions: this.instructions || null, addons: this.addons() }; this.saving.set(true); (this.id ? this.service.update(this.id, request) : this.service.create(request)).pipe(finalize(() => this.saving.set(false))).subscribe({ next: () => { this.reset(); this.load(); }, error: () => this.error.set('preparedMeals.saveError') }); }
  edit(meal: PreparedMeal) { this.id = meal.id; this.name = meal.name; this.brand = meal.brand ?? ''; this.servings = meal.servings; this.price = meal.price ?? null; this.instructions = meal.preparationInstructions ?? ''; this.addons.set(meal.addons.map(({ ingredientId, quantity, unit, isSelectedByDefault, isRequired, sortOrder }) => ({ ingredientId, quantity, unit, isSelectedByDefault, isRequired, sortOrder }))); this.loadImages(); }
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
  reset() { this.id = null; this.name = this.brand = this.instructions = ''; this.servings = 1; this.price = null; this.addons.set([]); this.images.set([]); this.clearImageUrls(); }
  private clearImageUrls() { Object.values(this.imageUrls()).forEach(URL.revokeObjectURL); this.imageUrls.set({}); }
}
