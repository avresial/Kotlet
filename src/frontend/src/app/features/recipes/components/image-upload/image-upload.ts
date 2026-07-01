import { ChangeDetectionStrategy, Component, inject, output, signal } from '@angular/core';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

@Component({ selector: 'app-image-upload', imports: [TranslatePipe], templateUrl: './image-upload.html', styleUrl: './image-upload.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class ImageUpload {
  private readonly translations = inject(TranslationService);
  readonly selected = output<{ file: File; altText: string }>();
  readonly error = signal<string | null>(null);
  altText = '';
  file: File | null = null;

  choose(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.error.set(null);
    if (!file) { this.file = null; return; }
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) { this.error.set(this.translations.translate('recipes.imageTypeError')); return; }
    if (file.size > 5 * 1024 * 1024) { this.error.set(this.translations.translate('recipes.imageSizeError')); return; }
    this.file = file;
  }
  submit(): void { if (this.file) this.selected.emit({ file: this.file, altText: this.altText }); }
}
