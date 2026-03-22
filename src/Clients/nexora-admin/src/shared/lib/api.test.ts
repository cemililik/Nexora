import axios from 'axios';
import { describe, expect, it } from 'vitest';

import { extractApiError } from './api';

describe('extractApiError', () => {
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

  it('should extract message and status from Axios error with envelope', () => {
    const error = new axios.AxiosError(
      'Bad Request',
      '400',
      undefined,
      undefined,
      {
        status: 400,
        statusText: 'Bad Request',
        headers: {},
        config: { headers: new axios.AxiosHeaders() },
        data: {
          data: null,
          message: 'lockey_error_validation_failed',
          meta: { field: 'email' },
          errors: [{ key: 'lockey_validation_email_invalid' }],
        },
      },
    );

    const result = extractApiError(error);
    expect(result.message).toBe('lockey_error_validation_failed');
    expect(result.status).toBe(400);
    expect(result.meta).toEqual({ field: 'email' });
    expect(result.errors).toEqual([{ key: 'lockey_validation_email_invalid' }]);
  });

  it('should fallback to lockey_error_unexpected when Axios error has no envelope', () => {
    const error = new axios.AxiosError(
      'Internal Server Error',
      '500',
      undefined,
      undefined,
      {
        status: 500,
        statusText: 'Internal Server Error',
        headers: {},
        config: { headers: new axios.AxiosHeaders() },
        data: undefined,
      },
    );

    const result = extractApiError(error);
    expect(result.message).toBe('lockey_error_unexpected');
    expect(result.status).toBe(500);
  });

  it('should handle Axios error without response (network error)', () => {
    const error = new axios.AxiosError(
      'Network Error',
      'ERR_NETWORK',
    );

    const result = extractApiError(error);
    expect(result.message).toBe('lockey_error_unexpected');
    expect(result.status).toBeUndefined();
  });
});
