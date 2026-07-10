import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ImageSourceAttribution } from '../../models/recipe.models';

/** Info icon overlaid on a recipe image; links to the attribution page of the image's source. */
@Component({
  selector: 'app-image-attribution',
  imports: [TranslatePipe],
  templateUrl: './image-attribution.html',
  styleUrl: './image-attribution.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImageAttribution {
  readonly source = input.required<ImageSourceAttribution | null>();
  /** Author page preferred over the source page; null hides the control entirely. */
  readonly href = computed(() => this.source()?.authorUrl || this.source()?.url || null);
  readonly tooltip = computed(() => {
    const source = this.source();
    return source ? [source.authorName, source.provider].filter(Boolean).join(' · ') : '';
  });
}
