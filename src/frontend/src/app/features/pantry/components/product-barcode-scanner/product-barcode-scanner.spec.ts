import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { BARCODE_DECODER_LOADER, BarcodeFrameDecoder, DecodedBarcode } from './barcode-decoder';
import { ProductBarcodeScanner } from './product-barcode-scanner';

interface FakeTrack {
  stop: ReturnType<typeof vi.fn>;
}

describe('ProductBarcodeScanner', () => {
  let loadDecoder: ReturnType<typeof vi.fn<() => Promise<BarcodeFrameDecoder>>>;
  let getUserMedia: ReturnType<typeof vi.fn>;
  let decode: ReturnType<typeof vi.fn<(video: HTMLVideoElement) => DecodedBarcode | null>>;
  let frameSize: { width: number; height: number };
  let setIntervalSpy: { mock: { results: { value: unknown }[] } };
  let clearIntervalSpy: { mock: { calls: unknown[][] } };

  // Angular schedules its own timers, so only count the scanner's polling intervals.
  function intervalsLeft(): number {
    return setIntervalSpy.mock.results
      .map((result) => result.value)
      .filter((id) => !clearIntervalSpy.mock.calls.some(([cleared]) => cleared === id)).length;
  }

  function createStream(): { stream: MediaStream; track: FakeTrack } {
    const track: FakeTrack = { stop: vi.fn() };
    return { stream: { getTracks: () => [track] } as unknown as MediaStream, track };
  }

  function deferred<T>() {
    let resolve: (value: T) => void = () => undefined;
    let reject: (reason: unknown) => void = () => undefined;
    const promise = new Promise<T>((promiseResolve, promiseReject) => {
      resolve = promiseResolve;
      reject = promiseReject;
    });
    return { promise, resolve, reject };
  }

  function stubCamera(): FakeTrack {
    const { stream, track } = createStream();
    getUserMedia = vi.fn(() => Promise.resolve(stream));
    Object.defineProperty(navigator, 'mediaDevices', {
      value: { getUserMedia },
      configurable: true,
    });
    return track;
  }

  function createScanner() {
    TestBed.configureTestingModule({
      providers: [
        { provide: BARCODE_DECODER_LOADER, useValue: loadDecoder },
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
    frameSize = { width: 640, height: 480 };
    vi.spyOn(HTMLVideoElement.prototype, 'videoWidth', 'get').mockImplementation(
      () => frameSize.width,
    );
    vi.spyOn(HTMLVideoElement.prototype, 'videoHeight', 'get').mockImplementation(
      () => frameSize.height,
    );
    decode = vi.fn(() => null);
    loadDecoder = vi.fn(() => Promise.resolve({ decode }));
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    Reflect.deleteProperty(navigator, 'mediaDevices');
  });

  it('works without BarcodeDetector and never falls back to a media API', async () => {
    const track = stubCamera();
    expect('BarcodeDetector' in globalThis).toBe(false);
    const { scanner } = createScanner();

    await scanner.start();

    expect(scanner.isSupported()).toBe(true);
    expect(scanner.isScanning()).toBe(true);
    scanner.stop();
    expect(track.stop).toHaveBeenCalledOnce();
  });

  it('explains the fallback and never touches the camera without getUserMedia', async () => {
    Object.defineProperty(navigator, 'mediaDevices', { value: {}, configurable: true });
    const { fixture, scanner } = createScanner();

    await scanner.start();
    fixture.detectChanges();

    expect(scanner.isSupported()).toBe(false);
    expect(loadDecoder).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('pantry.product.scanner.unsupported');
  });

  it('requests the camera only after an explicit start and releases it on stop', async () => {
    const track = stubCamera();
    const { scanner } = createScanner();
    expect(getUserMedia).not.toHaveBeenCalled();
    expect(loadDecoder).not.toHaveBeenCalled();

    await scanner.start();

    expect(getUserMedia).toHaveBeenCalledOnce();
    expect(getUserMedia).toHaveBeenCalledWith({
      video: { facingMode: { ideal: 'environment' } },
      audio: false,
    });
    expect(scanner.isScanning()).toBe(true);
    expect(intervalsLeft()).toBe(1);

    scanner.stop();

    expect(track.stop).toHaveBeenCalledOnce();
    expect(scanner.isScanning()).toBe(false);
    expect(intervalsLeft()).toBe(0);
  });

  it('sets muted and playsInline as properties for Safari before playing', async () => {
    stubCamera();
    const play = vi.spyOn(HTMLMediaElement.prototype, 'play');
    let mutedAtPlay: boolean | null = null;
    let playsInlineAtPlay: boolean | null = null;
    play.mockImplementation(function (this: HTMLVideoElement) {
      mutedAtPlay = this.muted;
      playsInlineAtPlay = this.playsInline;
      return Promise.resolve();
    });
    const { scanner } = createScanner();

    await scanner.start();

    expect(mutedAtPlay).toBe(true);
    expect(playsInlineAtPlay).toBe(true);
  });

  it('emits a decoded code once, releases the camera and does not retry', async () => {
    const track = stubCamera();
    decode.mockReturnValueOnce({ rawValue: '4006381333931', format: 'EAN_13' });
    const { scanner, detected } = createScanner();

    await scanner.start();
    await vi.advanceTimersByTimeAsync(300);
    await vi.advanceTimersByTimeAsync(3000);

    expect(detected).toEqual(['4006381333931']);
    expect(decode).toHaveBeenCalledOnce();
    expect(track.stop).toHaveBeenCalledOnce();
    expect(intervalsLeft()).toBe(0);
  });

  it('keeps scanning while frames contain no code', async () => {
    stubCamera();
    const { scanner, detected } = createScanner();

    await scanner.start();
    await vi.advanceTimersByTimeAsync(900);

    expect(decode).toHaveBeenCalledTimes(3);
    expect(detected).toEqual([]);
    expect(scanner.isScanning()).toBe(true);
    expect(scanner.errorKey()).toBeNull();
  });

  it.each([
    ['a bad checksum', { rawValue: '4006381333932', format: 'EAN_13' }],
    ['UPC-E', { rawValue: '01234565', format: 'UPC_E' }],
    ['a QR code', { rawValue: '4006381333931', format: 'QR_CODE' }],
    ['a short ITF code', { rawValue: '40063813', format: 'ITF' }],
  ])('rejects %s and keeps scanning', async (_name, rejected) => {
    stubCamera();
    decode.mockReturnValueOnce(rejected);
    const { scanner, detected } = createScanner();

    await scanner.start();
    await vi.advanceTimersByTimeAsync(300);

    expect(detected).toEqual([]);
    expect(scanner.isScanning()).toBe(true);
  });

  it.each([
    ['EAN-8', '96385074', 'EAN_8'],
    ['UPC-A', '036000291452', 'UPC_A'],
    ['ITF-14', '10012345678902', 'ITF'],
  ])('accepts %s', async (_name, rawValue, format) => {
    stubCamera();
    decode.mockReturnValueOnce({ rawValue, format });
    const { scanner, detected } = createScanner();

    await scanner.start();
    await vi.advanceTimersByTimeAsync(300);

    expect(detected).toEqual([rawValue]);
  });

  it('skips decoding while the video has no frame', async () => {
    stubCamera();
    frameSize = { width: 0, height: 0 };
    const { scanner } = createScanner();

    await scanner.start();
    await vi.advanceTimersByTimeAsync(600);

    expect(decode).not.toHaveBeenCalled();
    expect(scanner.isScanning()).toBe(true);
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

  it('reports a missing camera', async () => {
    stubCamera();
    getUserMedia.mockRejectedValueOnce(new DOMException('none', 'NotFoundError'));
    const { scanner } = createScanner();

    await scanner.start();

    expect(scanner.errorKey()).toBe('pantry.product.scanner.error.noCamera');
  });

  it('does not request the camera when the decoder fails to load, and allows a retry', async () => {
    const track = stubCamera();
    loadDecoder.mockRejectedValueOnce(new Error('chunk failed'));
    const { scanner } = createScanner();

    await scanner.start();

    expect(getUserMedia).not.toHaveBeenCalled();
    expect(scanner.errorKey()).toBe('pantry.product.scanner.error.failed');
    expect(scanner.isStarting()).toBe(false);

    await scanner.start();

    expect(scanner.errorKey()).toBeNull();
    expect(scanner.isScanning()).toBe(true);
    scanner.stop();
    expect(track.stop).toHaveBeenCalledOnce();
  });

  it('releases the camera when playback fails, and allows a retry', async () => {
    const track = stubCamera();
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockRejectedValueOnce(
      new DOMException('blocked', 'NotAllowedError'),
    );
    const { scanner } = createScanner();

    await scanner.start();

    expect(track.stop).toHaveBeenCalledOnce();
    expect(scanner.isScanning()).toBe(false);
    expect(scanner.isStarting()).toBe(false);
    expect(intervalsLeft()).toBe(0);

    await scanner.start();
    expect(scanner.isScanning()).toBe(true);
  });

  it('releases the camera when the decoder throws an unexpected error', async () => {
    const track = stubCamera();
    decode.mockImplementationOnce(() => {
      throw new Error('decoder crashed');
    });
    const { scanner } = createScanner();

    await scanner.start();
    await vi.advanceTimersByTimeAsync(300);

    expect(scanner.errorKey()).toBe('pantry.product.scanner.error.failed');
    expect(track.stop).toHaveBeenCalledOnce();
    expect(intervalsLeft()).toBe(0);
  });

  it('releases the camera when the component is destroyed mid-scan', async () => {
    const track = stubCamera();
    const { fixture, scanner } = createScanner();
    await scanner.start();

    fixture.destroy();

    expect(track.stop).toHaveBeenCalledOnce();
    expect(intervalsLeft()).toBe(0);
  });

  it('never requests the camera if stopped while the decoder is loading', async () => {
    stubCamera();
    const pendingLoad = deferred<{ decode: typeof decode }>();
    loadDecoder.mockReturnValueOnce(pendingLoad.promise);
    const { scanner } = createScanner();
    const starting = scanner.start();

    scanner.stop();
    pendingLoad.resolve({ decode });
    await starting;

    expect(getUserMedia).not.toHaveBeenCalled();
    expect(scanner.isStarting()).toBe(false);
    expect(scanner.isScanning()).toBe(false);
  });

  it('stops a stream that arrives after the user already cancelled', async () => {
    stubCamera();
    const late = createStream();
    const pendingStream = deferred<MediaStream>();
    getUserMedia.mockReturnValueOnce(pendingStream.promise);
    const { scanner } = createScanner();
    const starting = scanner.start();
    await vi.advanceTimersByTimeAsync(0);

    scanner.stop();
    pendingStream.resolve(late.stream);
    await starting;

    expect(late.track.stop).toHaveBeenCalledOnce();
  });

  it('does not start polling when stopped while playback is pending', async () => {
    const track = stubCamera();
    const pendingPlay = deferred<void>();
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockReturnValueOnce(pendingPlay.promise);
    const { fixture, scanner } = createScanner();
    const starting = scanner.start();
    await vi.advanceTimersByTimeAsync(0);
    fixture.detectChanges();

    // Safari must see the preview before playback finishes, or playback can stay suspended.
    expect(fixture.nativeElement.querySelector('video').hidden).toBe(false);
    expect(scanner.isStarting()).toBe(true);

    scanner.stop();
    pendingPlay.resolve();
    await starting;

    expect(track.stop).toHaveBeenCalledOnce();
    expect(scanner.isScanning()).toBe(false);
    expect(intervalsLeft()).toBe(0);
  });

  it('lets a restart win over a stale pending start without cross-talk', async () => {
    stubCamera();
    const stale = createStream();
    const fresh = createStream();
    const staleRequest = deferred<MediaStream>();
    getUserMedia
      .mockReturnValueOnce(staleRequest.promise)
      .mockReturnValueOnce(Promise.resolve(fresh.stream));
    decode.mockReturnValueOnce({ rawValue: '4006381333931', format: 'EAN_13' });
    const { scanner, detected } = createScanner();

    const firstStart = scanner.start();
    await vi.advanceTimersByTimeAsync(0);
    scanner.stop();
    await scanner.start();
    staleRequest.reject(new DOMException('late denial', 'NotAllowedError'));
    await firstStart;

    expect(scanner.errorKey()).toBeNull();
    expect(scanner.isScanning()).toBe(true);
    expect(intervalsLeft()).toBe(1);

    await vi.advanceTimersByTimeAsync(300);

    expect(detected).toEqual(['4006381333931']);
    expect(fresh.track.stop).toHaveBeenCalledOnce();
    expect(stale.track.stop).not.toHaveBeenCalled();
  });

  it('stops a stale stream that resolves after a restart took over', async () => {
    stubCamera();
    const stale = createStream();
    const fresh = createStream();
    const staleRequest = deferred<MediaStream>();
    getUserMedia
      .mockReturnValueOnce(staleRequest.promise)
      .mockReturnValueOnce(Promise.resolve(fresh.stream));
    const { scanner } = createScanner();

    const firstStart = scanner.start();
    await vi.advanceTimersByTimeAsync(0);
    scanner.stop();
    await scanner.start();
    staleRequest.resolve(stale.stream);
    await firstStart;

    expect(stale.track.stop).toHaveBeenCalledOnce();
    expect(fresh.track.stop).not.toHaveBeenCalled();
    expect(scanner.isScanning()).toBe(true);
    expect(intervalsLeft()).toBe(1);
  });
});
