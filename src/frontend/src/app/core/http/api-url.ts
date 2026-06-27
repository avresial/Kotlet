import { environment } from '../../../environments/environment';

export function apiUrl(path: `/api/${string}`): string {
  return `${environment.apiBaseUrl}${path}`;
}
