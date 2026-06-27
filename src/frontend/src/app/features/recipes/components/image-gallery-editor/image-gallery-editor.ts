import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { finalize, Observable } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { RecipeImage } from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';
import { ImageGallery } from '../image-gallery/image-gallery';
import { ImageUpload } from '../image-upload/image-upload';

@Component({
  selector: 'app-image-gallery-editor',
  imports: [ImageGallery, ImageUpload],
  templateUrl: './image-gallery-editor.html',
  styleUrl: './image-gallery-editor.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImageGalleryEditor implements OnInit {
  private readonly service = inject(RecipeService);
  readonly recipeId = input.required<string>();
  readonly images = signal<RecipeImage[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.service.listImages(this.recipeId()).subscribe({
      next: images => this.images.set(images),
      error: err => this.error.set(getApiError(err, 'Unable to load images.')),
    });
  }
  upload(value: { file: File; altText: string }): void {
    this.run(this.service.uploadImage(this.recipeId(), value.file, value.altText), () => this.reload());
  }
  saveAlt(image: RecipeImage, event: Event): void {
    const altText = (event.target as HTMLInputElement).value;
    this.run(this.service.updateImage(this.recipeId(), image.id, altText), () => this.reload());
  }
  remove(image: RecipeImage): void {
    if (window.confirm(`Delete ${image.fileName}?`)) this.run(this.service.deleteImage(this.recipeId(), image.id), () => this.reload());
  }
  move(index: number, delta: number): void {
    const reordered = [...this.images()];
    [reordered[index], reordered[index + delta]] = [reordered[index + delta], reordered[index]];
    this.run(this.service.reorderImages(this.recipeId(), reordered.map(i => i.id)), () => this.images.set(reordered));
  }
  private run(request: Observable<unknown>, success: () => void): void {
    this.busy.set(true); this.error.set(null);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: success,
      error: (err: unknown) => this.error.set(getApiError(err, 'Image operation failed.')),
    });
  }
}
