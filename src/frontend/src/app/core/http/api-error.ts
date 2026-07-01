import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  message?: string;
  errors?: Record<string, string[]>;
}

export function getApiError(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) return fallback;
  if (error.status === 0) return fallback;

  const problem = error.error as ProblemDetails | null;
  if (problem?.message) return problem.message;
  const validationError = problem?.errors
    && Object.values(problem.errors).flat().find((message) => message.trim().length > 0);
  return validationError ?? fallback;
}
