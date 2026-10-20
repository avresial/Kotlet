import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadBarcodeDecoder } from './barcode-decoder';

/**
 * EAN-13 4006381333931 as 95 modules (1 = bar). Built by hand from the GS1 L/G/R code tables and
 * the first-digit parity pattern, independently of ZXing, so decoding proves the real decoder works.
 */
const ean13Modules =
  '10100011010100111010111101111010001001011001101010100001010000101000010111010010000101100110101';

const pixelsPerModule = 3;
const quietZoneModules = 10;
const frameHeight = 60;

function buildFrame(modules: string): { width: number; height: number; data: Uint8ClampedArray } {
  const columns = [
    ...'0'.repeat(quietZoneModules),
    ...modules,
    ...'0'.repeat(quietZoneModules),
  ].flatMap((module) => Array<string>(pixelsPerModule).fill(module));
  const width = columns.length;
  const data = new Uint8ClampedArray(width * frameHeight * 4);
  for (let row = 0; row < frameHeight; row++) {
    columns.forEach((module, column) => {
      const offset = (row * width + column) * 4;
      const shade = module === '1' ? 0 : 255;
      data.set([shade, shade, shade, 255], offset);
    });
  }
  return { width, height: frameHeight, data };
}

function stubCanvas(frame: { width: number; height: number; data: Uint8ClampedArray }): void {
  const context = {
    drawImage: vi.fn(),
    translate: vi.fn(),
    rotate: vi.fn(),
    getImageData: (_x: number, _y: number, width: number, height: number) =>
      width === frame.width && height === frame.height
        ? { data: frame.data, width, height }
        : { data: new Uint8ClampedArray(width * height * 4).fill(255), width, height },
  };
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockImplementation(
    (() => context) as unknown as HTMLCanvasElement['getContext'],
  );
}

function videoOfSize(width: number, height: number): HTMLVideoElement {
  const video = document.createElement('video');
  Object.defineProperty(video, 'videoWidth', { value: width });
  Object.defineProperty(video, 'videoHeight', { value: height });
  return video;
}

describe('loadBarcodeDecoder', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('decodes a real EAN-13 pixel fixture', async () => {
    expect(ean13Modules).toHaveLength(95);
    const frame = buildFrame(ean13Modules);
    stubCanvas(frame);
    const decoder = await loadBarcodeDecoder();

    const decoded = decoder.decode(videoOfSize(frame.width, frame.height));

    expect(decoded).toEqual({ rawValue: '4006381333931', format: 'EAN_13' });
  });

  it('returns null for a frame without a barcode', async () => {
    const blank = {
      width: 300,
      height: frameHeight,
      data: new Uint8ClampedArray(300 * frameHeight * 4).fill(255),
    };
    stubCanvas(blank);
    const decoder = await loadBarcodeDecoder();

    expect(decoder.decode(videoOfSize(blank.width, blank.height))).toBeNull();
  });

  it('returns null instead of throwing when the check digit is wrong', async () => {
    // Flip the last data digit from 1 (R code 1100110) to 2 (1101100) so the EAN-13 checksum fails.
    const corrupted = ean13Modules.replace(/1100110101$/, '1101100101');
    expect(corrupted).not.toBe(ean13Modules);
    const frame = buildFrame(corrupted);
    stubCanvas(frame);
    const decoder = await loadBarcodeDecoder();

    expect(decoder.decode(videoOfSize(frame.width, frame.height))).toBeNull();
  });

  it('surfaces unexpected decoder errors', async () => {
    const frame = buildFrame(ean13Modules);
    stubCanvas(frame);
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
    const decoder = await loadBarcodeDecoder();

    expect(() => decoder.decode(videoOfSize(frame.width, frame.height))).toThrow();
  });
});
