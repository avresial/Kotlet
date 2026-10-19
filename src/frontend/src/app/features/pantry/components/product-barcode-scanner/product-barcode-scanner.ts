import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { toGtin14 } from '../../barcode';

interface DetectedBarcode {
  rawValue: string;
  format: string;
}

interface BarcodeDetectorLike {
  detect(source: HTMLVideoElement): Promise<DetectedBarcode[]>;
}

interface BarcodeDetectorConstructor {
  new (options?: { formats: string[] }): BarcodeDetectorLike;
  getSupportedFormats?(): Promise<string[]>;
}

/** Retail barcode formats the product lookup understands. */
export const scannableFormats = ['ean_13', 'ean_8', 'upc_a', 'itf'];

const scanIntervalMilliseconds = 300;

function barcodeDetectorConstructor(): BarcodeDetectorConstructor | undefined {
  return (globalThis as { BarcodeDetector?: BarcodeDetectorConstructor }).BarcodeDetector;
}

function isCameraSupported(): boolean {
  return (
    barcodeDetectorConstructor() !== undefined &&
    typeof navigator !== 'undefined' &&
    typeof navigator.mediaDevices?.getUserMedia === 'function'
  );
}

/**
 * Camera barcode scanner built on the native BarcodeDetector. The camera is only requested after
 * an explicit click, and every exit path (stop, failure, detection, destroy) releases it.
 */
@Component({
  selector: 'app-product-barcode-scanner',
  imports: [TranslatePipe],
  templateUrl: './product-barcode-scanner.html',
  styleUrl: './product-barcode-scanner.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductBarcodeScanner implements OnDestroy {
  readonly codeDetected = output<string>();
  readonly isSupported = signal(isCameraSupported());
  readonly isStarting = signal(false);
  readonly isScanning = signal(false);
  /** Translation key describing why the camera could not be used, or null. */
  readonly errorKey = signal<string | null>(null);

  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');
  private stream: MediaStream | null = null;
  private timer: ReturnType<typeof setInterval> | null = null;
  private isDetecting = false;
  /** Incremented whenever a session ends so late async results from older sessions are dropped. */
  private session = 0;

  async start(): Promise<void> {
    if (this.isStarting() || this.isScanning()) {
      return;
    }
    const detectorType = barcodeDetectorConstructor();
    if (!detectorType || !isCameraSupported()) {
      this.isSupported.set(false);
      return;
    }

    const session = ++this.session;
    this.errorKey.set(null);
    this.isStarting.set(true);
    try {
      const formats = await this.supportedFormats(detectorType);
      if (session !== this.session) {
        return;
      }
      if (formats.length === 0) {
        throw new UnsupportedFormatsError();
      }
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: { ideal: 'environment' } },
        audio: false,
      });
      if (session !== this.session) {
        stopTracks(stream);
        return;
      }
      this.stream = stream;
      this.isScanning.set(true);
      await this.attachStream(stream);
      if (session !== this.session) {
        return;
      }
      const detector = new detectorType({ formats });
      this.timer = setInterval(
        () => void this.scanFrame(detector, session),
        scanIntervalMilliseconds,
      );
    } catch (error) {
      if (session === this.session) {
        this.releaseCamera();
        this.isStarting.set(false);
        this.errorKey.set(cameraErrorKey(error));
      }
    } finally {
      if (session === this.session) {
        this.isStarting.set(false);
      }
    }
  }

  stop(): void {
    this.releaseCamera();
    this.isStarting.set(false);
  }

  ngOnDestroy(): void {
    this.releaseCamera();
  }

  private async supportedFormats(detectorType: BarcodeDetectorConstructor): Promise<string[]> {
    if (!detectorType.getSupportedFormats) {
      return scannableFormats;
    }
    const available = await detectorType.getSupportedFormats();
    return scannableFormats.filter((format) => available.includes(format));
  }

  private async attachStream(stream: MediaStream): Promise<void> {
    const element = this.video()?.nativeElement;
    if (!element) {
      return;
    }
    element.srcObject = stream;
    await element.play();
  }

  private async scanFrame(detector: BarcodeDetectorLike, session: number): Promise<void> {
    const element = this.video()?.nativeElement;
    if (this.isDetecting || !element || session !== this.session) {
      return;
    }
    this.isDetecting = true;
    try {
      const barcodes = await detector.detect(element);
      if (session !== this.session) {
        return;
      }
      const code = firstScannableCode(barcodes);
      if (code) {
        this.releaseCamera();
        this.codeDetected.emit(code);
      }
    } catch {
      if (session === this.session) {
        this.releaseCamera();
        this.errorKey.set('pantry.product.scanner.error.failed');
      }
    } finally {
      this.isDetecting = false;
    }
  }

  private releaseCamera(): void {
    this.session++;
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
    if (this.stream) {
      stopTracks(this.stream);
      this.stream = null;
    }
    const element = this.video()?.nativeElement;
    if (element) {
      element.pause();
      element.srcObject = null;
    }
    this.isScanning.set(false);
  }
}

class UnsupportedFormatsError extends Error {}

function stopTracks(stream: MediaStream): void {
  for (const track of stream.getTracks()) {
    track.stop();
  }
}

function firstScannableCode(barcodes: DetectedBarcode[]): string | null {
  for (const barcode of barcodes) {
    const rawValue = barcode.rawValue.trim();
    if (!scannableFormats.includes(barcode.format) || !toGtin14(rawValue)) {
      continue;
    }
    return rawValue;
  }
  return null;
}

function cameraErrorKey(error: unknown): string {
  if (error instanceof UnsupportedFormatsError) {
    return 'pantry.product.scanner.error.formats';
  }
  const name = error instanceof DOMException ? error.name : '';
  if (name === 'NotAllowedError' || name === 'SecurityError') {
    return 'pantry.product.scanner.error.denied';
  }
  if (name === 'NotFoundError' || name === 'OverconstrainedError') {
    return 'pantry.product.scanner.error.noCamera';
  }
  return 'pantry.product.scanner.error.failed';
}
