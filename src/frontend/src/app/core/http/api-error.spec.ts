import { HttpErrorResponse } from '@angular/common/http';
import { getApiError } from './api-error';

describe('getApiError', () => {
  it('uses RFC7807 detail when the API does not return a message', () => {
    const error = new HttpErrorResponse({ status: 502, error: { detail: 'Provider failed.' } });

    expect(getApiError(error, 'Fallback')).toBe('Provider failed.');
  });
});
