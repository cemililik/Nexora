import { describe, expect, it, vi, beforeEach } from 'vitest';

import { extractApiError } from './api';

// Only test extractApiError — the api client methods require full Axios mocking
// which is better suited for integration tests.

describe('extractApiError', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should return lockey_error_unexpected for non-Axios errors', () => {
    const result = extractApiError(new Error('some error'));
    expect(result.message).toBe('lockey_error_unexpected');
    expect(result.status).toBeUndefined();
  });

  it('should return lockey_error_unexpected for null errors', () => {
    const result = extractApiError(null);
    expect(result.message).toBe('lockey_error_unexpected');
  });

  it('should return lockey_error_unexpected for string errors', () => {
    const result = extractApiError('something broke');
    expect(result.message).toBe('lockey_error_unexpected');
  });
});
