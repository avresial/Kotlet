import { InjectionToken } from '@angular/core';

/** Retail barcode formats the product lookup understands. UPC-E and QR codes are intentionally excluded. */
export const scannableFormats = ['EAN_13', 'EAN_8', 'UPC_A', 'ITF'] as const;

export interface DecodedBarcode {
  rawValue: string;
  /** ZXing `BarcodeFormat` name, for example `EAN_13`. */
  format: string;
}

export interface BarcodeFrameDecoder {
  /**
   * Synchronously decodes the current video frame. Returns null when the frame holds no readable
   * barcode, which is the normal case for most frames. Any thrown error is unexpected and fatal.
   */
  decode(video: HTMLVideoElement): DecodedBarcode | null;
}

/**
 * Loads the ZXing decoder on first use so it stays out of the main bundle. The same software decoder
 * is used in every browser, which avoids depending on the native BarcodeDetector.
 */
export async function loadBarcodeDecoder(): Promise<BarcodeFrameDecoder> {
  const [{ BrowserMultiFormatOneDReader }, zxing] = await Promise.all([
    import('@zxing/browser'),
    import('@zxing/library'),
  ]);
  const { BarcodeFormat, ChecksumException, DecodeHintType, FormatException, NotFoundException } =
    zxing;
  // TRY_HARDER is deliberately not set: it makes ZXing rotate empty frames through a canvas helper
  // in @zxing/browser that throws, which would turn every frame without a code into a fatal error.
  const hints = new Map<number, unknown>([
    [
      DecodeHintType.POSSIBLE_FORMATS,
      [BarcodeFormat.EAN_13, BarcodeFormat.EAN_8, BarcodeFormat.UPC_A, BarcodeFormat.ITF],
    ],
  ]);
  const reader = new BrowserMultiFormatOneDReader(hints);

  return {
    decode(video) {
      try {
        const result = reader.decode(video);
        return { rawValue: result.getText(), format: BarcodeFormat[result.getBarcodeFormat()] };
      } catch (error) {
        const isExpectedMiss =
          error instanceof NotFoundException ||
          error instanceof ChecksumException ||
          error instanceof FormatException;
        if (isExpectedMiss) {
          return null;
        }
        throw error;
      }
    },
  };
}

/** Injectable so component tests can substitute the decoder without loading ZXing. */
export const BARCODE_DECODER_LOADER = new InjectionToken<() => Promise<BarcodeFrameDecoder>>(
  'BARCODE_DECODER_LOADER',
  { providedIn: 'root', factory: () => loadBarcodeDecoder },
);
