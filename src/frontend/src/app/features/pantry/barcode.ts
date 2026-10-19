const supportedLengths = [8, 12, 13, 14];

/** Removes the spaces and hyphens people type or paste into barcode fields. */
export function cleanBarcodeInput(value: string): string {
  return value.replace(/[\s-]/g, '');
}

function hasValidCheckDigit(digits: string): boolean {
  const body = digits.slice(0, -1);
  let sum = 0;
  for (let index = 0; index < body.length; index++) {
    const digit = Number(body[body.length - 1 - index]);
    sum += index % 2 === 0 ? digit * 3 : digit;
  }
  return (10 - (sum % 10)) % 10 === Number(digits[digits.length - 1]);
}

/**
 * Mirrors the backend rules: EAN-8, UPC-A, EAN-13 and GTIN-14 with a valid check digit.
 * Returns the canonical GTIN-14, or null when the code is not acceptable.
 */
export function toGtin14(value: string): string | null {
  const digits = cleanBarcodeInput(value);
  if (!/^\d+$/.test(digits) || !supportedLengths.includes(digits.length) || /^0+$/.test(digits)) {
    return null;
  }
  return hasValidCheckDigit(digits) ? digits.padStart(14, '0') : null;
}
