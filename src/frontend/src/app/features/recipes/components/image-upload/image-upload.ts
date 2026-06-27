import { ChangeDetectionStrategy, Component, output, signal } from '@angular/core';

@Component({ selector: 'app-image-upload', templateUrl: './image-upload.html', styleUrl: './image-upload.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class ImageUpload {
  readonly selected = output<{ file: File; altText: string }>();
  readonly error = signal<string | null>(null);
  altText = '';
  file: File | null = null;

  choose(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.error.set(null);
    if (!file) { this.file = null; return; }
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) { this.error.set('Choose a JPEG, PNG, or WebP image.'); return; }
    if (file.size > 5 * 1024 * 1024) { this.error.set('Image cannot exceed 5 MB.'); return; }
    this.file = file;
  }
  submit(): void { if (this.file) this.selected.emit({ file: this.file, altText: this.altText }); }
}
