import { describe, expect, it } from 'vitest';
import { ApiError, errorMessage, isDuplicateConflictError, newIdempotencyKey } from './api';

describe('errorMessage', () => {
  it('reads the server error envelope first', () => {
    const error = new ApiError('Request failed (409).', 409, { error: 'Location code already exists.' });
    expect(errorMessage(error)).toBe('Location code already exists.');
  });

  it('joins model validation errors when there is no envelope', () => {
    const error = new ApiError('Request failed (400).', 400, {
      errors: { SerialNumber: ['Serial Number is required.'], TagNumber: ['Tag Number is required.'] },
    });
    expect(errorMessage(error)).toBe('Serial Number is required. Tag Number is required.');
  });

  it('falls back to the ProblemDetails title', () => {
    expect(errorMessage(new ApiError('x', 400, { title: 'Bad request' }))).toBe('Bad request');
  });

  it('uses the error message, then the fallback', () => {
    expect(errorMessage(new Error('Network down'))).toBe('Network down');
    expect(errorMessage('not an error', 'Try again.')).toBe('Try again.');
  });

  it('exposes the status for callers that branch on it', () => {
    expect(new ApiError('x', 404, undefined).status).toBe(404);
  });
});

describe('isDuplicateConflictError', () => {
  const conflict = {
    errorCode: 'DUPLICATE_IDENTIFIER',
    message: "Serial Number 'F1' is already registered in the system.",
    conflictingField: 'SerialNumber',
    conflictingValue: 'F1',
    existingItem: {},
  };

  it('recognises the duplicate body the register endpoint returns', () => {
    expect(isDuplicateConflictError(conflict)).toBe(true);
  });

  it('rejects anything else', () => {
    expect(isDuplicateConflictError({ ...conflict, errorCode: 'OTHER' })).toBe(false);
    expect(isDuplicateConflictError({ ...conflict, conflictingField: 'Name' })).toBe(false);
    expect(isDuplicateConflictError({ error: 'x' })).toBe(false);
    expect(isDuplicateConflictError(null)).toBe(false);
  });
});

describe('newIdempotencyKey', () => {
  it('returns a different key on every call that fits the 64-character column', () => {
    const keys = new Set(Array.from({ length: 100 }, () => newIdempotencyKey()));
    expect(keys.size).toBe(100);
    for (const key of keys) expect(key.length).toBeLessThanOrEqual(64);
  });
});
