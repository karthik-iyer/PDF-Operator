import { HttpErrorResponse } from '@angular/common/http';

/**
 * Extracts a human-readable message from a backend error response body.
 * Handles the `{ "error": "..." }` shape used by NETCore-PDF-Service's Minimal API
 * validation errors (see DocumentEndpoints.cs), plain-text bodies, and falls back to a
 * caller-supplied default for anything else (e.g. ProblemDetails responses, network errors).
 */
export function extractErrorMessage(err: HttpErrorResponse, fallback: string): string {
  const body = err.error as { error?: string } | string | null;
  if (typeof body === 'string' && body.trim().length > 0) {
    return body;
  }
  if (body && typeof body === 'object' && typeof body.error === 'string') {
    return body.error;
  }
  return fallback;
}
