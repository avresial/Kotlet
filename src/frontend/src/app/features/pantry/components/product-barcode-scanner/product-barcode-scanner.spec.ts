import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { ProductBarcodeScanner } from './product-barcode-scanner';

interface FakeBarcode {
  rawValue: string;
  format: string;
}

describe('ProductBarcodeScanner', () => {
  let track: { stop: ReturnType<typeof vi.fn> };
  let getUserMedia: ReturnType<typeof vi.fn>;
  let detect: ReturnType<typeof vi.fn>;
  let detectorOptions: { formats: string[] }[];
  let setIntervalSpy: { mock: { results: { value: unknown }[] } };
  let clearIntervalSpy: { mock: { calls: unknown[][] } };

  // Angular schedules its own timers, so only count the scanner's polling intervals.
  function intervalsLeft(): number {
    return setIntervalSpy.mock.results
      .map((result) => result.value)
      .filter((id) => !clearIntervalSpy.mock.calls.some(([cleared]) => cleared === id)).length;
  }

  function stubCamera(): void {
    track = { stop: vi.fn() };
    const stream = { getTracks: () => [track] } as unknown as MediaStream;
    getUserMedia = vi.fn(() => Promise.resolve(stream));
    Object.defineProperty(navigator, 'mediaDevices', {
      value: { getUserMedia },
      configurable: true,
    });
    detect = vi.fn(() => Promise.resolve([] as FakeBarcode[]));
    detectorOptions = [];
    vi.stubGlobal(
      'BarcodeDetector',
      class {
        static getSupportedFormats = () =>
          Promise.resolve(['ean_13', 'ean_8', 'upc_a', 'upc_e', 'itf', 'qr_code']);
        constructor(options: { formats: string[] }) {
          detectorOptions.push(options);
        }
        detect = detect;
      },
    );
  }

  function createScanner() {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: TranslationService,
          useValue: { language: signal('en'), translate: (key: string) => key },
        },
      ],
    });
    const fixture = TestBed.createComponent(ProductBarcodeScanner);
    fixture.detectChanges();
    const detected: string[] = [];
    fixture.componentInstance.codeDetected.subscribe((code) => detected.push(code));
    return { fixture, scanner: fixture.componentInstance, detected };
  }

  beforeEach(() => {
    vi.useFakeTimers();
    setIntervalSpy = vi.spyOn(globalThis, 'setInterval');
    clearIntervalSpy = vi.spyOn(globalThis, 'clearInterval');
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue(undefined);
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => undefined);
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    Reflect.deleteProperty(navigator, 'mediaDevices');
  });

  it('explains the fallback and never touches the camera when BarcodeDetector is missing', async () => {
    Object.defineProperty(navigator, 'mediaDevices', {
      value: { getUserMedia: vi.fn() },
      configurable: true,
    });
    const { fixture, scanner } = createScanner();

    await scanner.start();
    fixture.detectChanges();

    expect(scanner.isSupported()).toBe(false);
    expect(navigator.mediaDevices.getUserMedia).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('pantry.product.scanner.unsupported');
  });

  it('requests the camera only after an explicit start and releases it on stop', async () => {
    stubCamera();
    const { scanner } = createScanner();
    expect(getUserMedia).not.toHaveBeenCalled();

    await scanner.start();

    expect(getUserMedia).toHaveBeenCalledOnce();
    expect(detectorOptions[0].formats).toEqual(['ean_13', 'ean_8', 'upc_a', 'itf']);
    expect(scanner.isScanning()).toBe(true);
    expect(intervalsLeft()).toBe(1);

    scanner.stop();

    expect(track.stop).toHaveBeenCalledOnce();
    expect(scanner.isScanning()).toBe(false);
    expect(intervalsLeft()).toBe(0);
  });

  it('emits the detected code and releases the camera', async () => {
    stubCamera();
    detect.mockResolvedValueOnce([{ rawValue: '4006381333931', format: 'ean_13' }]);
    const { scanner, detected } = createScanner();

    await scanner.start();
    await vi.advanceTimersByTimeAsync(300);

    expect(detected).toEqual(['4006381333931']);
    expect(track.stop).toHaveBeenCalledOnce();
    expect(intervalsLeft()).toBe(0);
  });

  it('ignores unsupported UPC-E detections', async () => {
    stubCamera();
    detect.mockResolvedValueOnce([{ rawValue: '01234565', format: 'upc_e' }]);
    const { scanner, detected } = createScanner();

    await scanner.start();
    await vi.advanceTimersByTimeAsync(300);

    expect(detected).toEqual([]);
  });

  it('reports a denied permission and leaves no running timer or track', async () => {
    stubCamera();
    getUserMedia.mockRejectedValueOnce(new DOMException('denied', 'NotAllowedError'));
    const { scanner } = createScanner();

    await scanner.start();

    expect(scanner.errorKey()).toBe('pantry.product.scanner.error.denied');
    expect(scanner.isStarting()).toBe(false);
    expect(scanner.isScanning()).toBe(false);
    expect(intervalsLeft()).toBe(0);
  });

  it('releases the camera when detection fails', async () => {
    stubCamera();
    detect.mockRejectedValueOnce(new Error('decoder crashed'));
    const { scanner } = createScanner();

    await scanner.start();
    await vi.advanceTimersByTimeAsync(300);

    expect(scanner.errorKey()).toBe('pantry.product.scanner.error.failed');
    expect(track.stop).toHaveBeenCalledOnce();
    expect(intervalsLeft()).toBe(0);
  });

  it('releases the camera when the component is destroyed mid-scan', async () => {
    stubCamera();
    const { fixture, scanner } = createScanner();
    await scanner.start();

    fixture.destroy();

    expect(track.stop).toHaveBeenCalledOnce();
    expect(intervalsLeft()).toBe(0);
  });

  it('never requests the camera if stopped while formats are loading', async () => {
    stubCamera();
    let resolveFormats: (formats: string[]) => void = () => undefined;
    const pendingFormats = new Promise<string[]>((resolve) => {
      resolveFormats = resolve;
    });
    vi.stubGlobal(
      'BarcodeDetector',
      class {
        static getSupportedFormats = () => pendingFormats;
        detect = detect;
      },
    );
    const { scanner } = createScanner();
    const starting = scanner.start();
    scanner.stop();
    resolveFormats(['ean_13']);
    await starting;
    expect(getUserMedia).not.toHaveBeenCalled();
    expect(scanner.isStarting()).toBe(false);
  });

  it('stops a stream that arrives after the user already cancelled', async () => {
    stubCamera();
    let resolveStream: (stream: MediaStream) => void = () => undefined;
    getUserMedia.mockReturnValueOnce(
      new Promise<MediaStream>((resolve) => {
        resolveStream = resolve;
      }),
    );
    const { scanner } = createScanner();
    const starting = scanner.start();
    await vi.advanceTimersByTimeAsync(0);

    scanner.stop();
    resolveStream({ getTracks: () => [track] } as unknown as MediaStream);
    await starting;

    expect(track.stop).toHaveBeenCalledOnce();
    expect(scanner.isScanning()).toBe(false);
    expect(intervalsLeft()).toBe(0);
  });
});
