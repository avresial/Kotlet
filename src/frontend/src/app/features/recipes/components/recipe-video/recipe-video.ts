import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-recipe-video',
  imports: [TranslatePipe],
  templateUrl: './recipe-video.html',
  styleUrl: './recipe-video.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecipeVideo {
  readonly videoUrl = input.required<string>();
  readonly thumbnailUrl = input<string | null>(null);
  readonly title = input.required<string>();
  readonly hasStarted = signal(false);

  play(video: HTMLVideoElement): void {
    this.hasStarted.set(true);
    void video.play().catch(() => this.hasStarted.set(false));
  }
}
