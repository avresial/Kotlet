import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { toGtin14 } from '../../barcode';
import {
  BARCODE_DECODER_LOADER,
  BarcodeFrameDecoder,
  DecodedBarcode,
  scannableFormats,
} from './barcode-decoder';

const scanIntervalMilliseconds = 300;
const itfGtin14Length = 14;

function isCameraSupported(): boolean {
  return (
    typeof navigator !== 'undefined' && typeof navigator.mediaDevices?.getUserMedia === 'function'
  );
}

/**
 * Camera barcode scanner that decodes video frames with a lazily loaded software decoder, so it
 * behaves the same in every browser. The camera is only requested after an explicit click, and every
 * exit path (stop, failure, detection, destroy) releases it.
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

  private readonly loadDecoder = inject(BARCODE_DECODER_LOADER);
  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');
  private stream: MediaStream | null = null;
  private timer: ReturnType<typeof setInterval> | null = null;
  /**
   * Incremented whenever a session starts or ends, so late async results (module load, camera
   * permission, playback) from an older session are dropped. Decoding is synchronous, so a frame can
   * never outlive its session and needs no separate busy guard.
   */
  private session = 0;

  async start(): Promise<void> {
    if (this.isStarting() || this.isScanning()) {
      return;
    }
    if (!isCameraSupported()) {
      this.isSupported.set(false);
      return;
    }

    const session = ++this.session;
    this.errorKey.set(null);
    this.isStarting.set(true);
    try {
      const decoder = await this.loadDecoder();
      if (session !== this.session) {
        return;
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
      await this.attachStream(stream);
      if (session !== this.session) {
        return;
      }
      this.isStarting.set(false);
      this.isScanning.set(true);
      this.timer = setInterval(() => this.scanFrame(decoder, session), scanIntervalMilliseconds);
    } catch (error) {
      if (session === this.session) {
        this.releaseCamera();
        this.errorKey.set(cameraErrorKey(error));
      }
    }
  }

  stop(): void {
    this.releaseCamera();
  }

  ngOnDestroy(): void {
    this.releaseCamera();
  }

  private async attachStream(stream: MediaStream): Promise<void> {
    const element = this.video()?.nativeElement;
    if (!element) {
      throw new Error('The scanner video element is missing.');
    }
    // Safari only plays inline without a gesture when these are set as properties, not just attributes.
    element.muted = true;
    element.playsInline = true;
    element.srcObject = stream;
    await element.play();
  }

  private scanFrame(decoder: BarcodeFrameDecoder, session: number): void {
    const element = this.video()?.nativeElement;
    if (session !== this.session || !element) {
      return;
    }
    const hasFrame = element.videoWidth > 0 && element.videoHeight > 0;
    if (!hasFrame) {
      return;
    }

    let decoded: DecodedBarcode | null;
    try {
      decoded = decoder.decode(element);
    } catch {
      this.releaseCamera();
      this.errorKey.set('pantry.product.scanner.error.failed');
      return;
    }
    const code = decoded ? acceptedCode(decoded) : null;
    if (code) {
      this.releaseCamera();
      this.codeDetected.emit(code);
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
    this.isStarting.set(false);
    this.isScanning.set(false);
  }
}

function stopTracks(stream: MediaStream): void {
  for (const track of stream.getTracks()) {
    track.stop();
  }
}

function acceptedCode(barcode: DecodedBarcode): string | null {
  const rawValue = barcode.rawValue.trim();
  const isScannableFormat = (scannableFormats as readonly string[]).includes(barcode.format);
  // ITF encodes any even digit count, so only a full GTIN-14 is a retail ITF-14 code.
  const isValidItfLength = barcode.format !== 'ITF' || rawValue.length === itfGtin14Length;
  if (!isScannableFormat || !isValidItfLength || !toGtin14(rawValue)) {
    return null;
  }
  return rawValue;
}

function cameraErrorKey(error: unknown): string {
  const name = error instanceof DOMException ? error.name : '';
  if (name === 'NotAllowedError' || name === 'SecurityError') {
    return 'pantry.product.scanner.error.denied';
  }
  if (name === 'NotFoundError' || name === 'OverconstrainedError') {
    return 'pantry.product.scanner.error.noCamera';
  }
  return 'pantry.product.scanner.error.failed';
}
