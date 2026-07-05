import { ChangeDetectionStrategy, Component, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

@Component({ selector: 'app-image-upload', imports: [TranslatePipe], templateUrl: './image-upload.html', styleUrl: './image-upload.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class ImageUpload {
  private readonly translations = inject(TranslationService);
  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
  readonly selected = output<{ file: File; altText: string }>();
  /** Upload progress percentage (0-100), or null when no upload is in flight. */
  readonly progress = input<number | null>(null);
  readonly busy = input(false);
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
  reset(): void {
    this.file = null;
    this.altText = '';
    this.error.set(null);
    this.fileInput().nativeElement.value = '';
  }
}
