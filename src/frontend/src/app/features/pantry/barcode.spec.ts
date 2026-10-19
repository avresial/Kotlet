import { describe, expect, it } from 'vitest';
import { cleanBarcodeInput, toGtin14 } from './barcode';

describe('toGtin14', () => {
  it.each([
    ['4006381333931', '04006381333931'],
    ['036000291452', '00036000291452'],
    ['96385074', '00000096385074'],
    ['04006381333931', '04006381333931'],
  ])('canonicalizes %s', (code, expected) => {
    expect(toGtin14(code)).toBe(expected);
  });

  it('ignores spaces and hyphens typed into the field', () => {
    expect(cleanBarcodeInput(' 4006-381 333931 ')).toBe('4006381333931');
    expect(toGtin14(' 4006-381 333931 ')).toBe('04006381333931');
  });

  it.each([
    '00000000',
    '00000000000000',
    '4006381333932',
    '',
    'abc',
    '12345',
    '400638133393123',
    '4006381333 93x',
  ])('rejects %s', (code) => {
    expect(toGtin14(code)).toBeNull();
  });
});
