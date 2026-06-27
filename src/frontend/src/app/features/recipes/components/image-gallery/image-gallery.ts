import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RecipeImage } from '../../models/recipe.models';
import { RecipeService } from '../../services/recipe.service';

@Component({
  selector: 'app-image-gallery',
  templateUrl: './image-gallery.html',
  styleUrl: './image-gallery.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImageGallery {
  private readonly service = inject(RecipeService);
  private readonly destroyRef = inject(DestroyRef);
  readonly images = input.required<RecipeImage[]>();
  readonly urls = signal<Record<string, string>>({});

  constructor() {
    effect(() => {
      const images = this.images();
      untracked(() => this.revokeUrls());
      for (const image of images) {
        this.service.imageContent(image.contentUrl).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(blob => {
          const url = URL.createObjectURL(blob);
          this.urls.update(urls => ({ ...urls, [image.id]: url }));
        });
      }
    });
    this.destroyRef.onDestroy(() => this.revokeUrls());
  }

  private revokeUrls(): void {
    Object.values(this.urls()).forEach(url => URL.revokeObjectURL(url));
    this.urls.set({});
  }
}
