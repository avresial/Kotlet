# Camera scanner browser compatibility

- The pantry barcode scanner no longer needs the native `BarcodeDetector`, which Safari and some Chrome builds lack.
- Frames are decoded by a bundled software decoder (`@zxing/browser` + `@zxing/library`) that is lazy-loaded when the user presses the scan button, so it is not part of the initial bundle.
- The same decoder runs in every browser. Camera support only requires `getUserMedia` on HTTPS or localhost.
- Supported codes are unchanged: EAN-8, EAN-13, UPC-A and ITF-14, still validated by `toGtin14`. UPC-E and QR codes are ignored.
- The camera starts only after an explicit click. It is released on stop, a successful scan, any error, and component destroy; late permission, module-load or playback results from a cancelled attempt are discarded, and the user can retry immediately.
- The video preview is visible while playback starts, with `muted` and `playsInline` set as properties for Safari.
- Frames without a readable code, or with a bad checksum, are normal and keep scanning; any other decoder failure releases the camera and shows the existing "Scanning failed" message.
- Known limit: only horizontally oriented 1D codes are read (ZXing's rotate-and-retry mode is disabled because it fails in `@zxing/browser`).

See [the feature and operating notes](../product-catalog.md).
