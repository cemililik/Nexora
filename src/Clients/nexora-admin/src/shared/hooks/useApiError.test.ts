import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';

// Mock extractApiError
const mockExtractApiError = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  extractApiError: (...args: unknown[]) => mockExtractApiError(...args),
}));

// Mock sonner
const mockToastError = vi.fn();
vi.mock('sonner', () => ({
  toast: { error: (...args: unknown[]) => mockToastError(...args) },
}));

// Mock react-i18next
const mockT = vi.fn((key: string) => key);
vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: mockT }),
}));

import { useApiError } from './useApiError';

describe('useApiError', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should show toast with translated message for general errors', () => {
    mockExtractApiError.mockReturnValue({
      message: 'lockey_error_unexpected',
      meta: undefined,
      errors: undefined,
    });

    const { result } = renderHook(() => useApiError());
    result.current.handleApiError(new Error('something'));

    expect(mockToastError).toHaveBeenCalledWith('lockey_error_unexpected');
    expect(mockT).toHaveBeenCalledWith('lockey_error_unexpected', {});
  });

  it('should pass meta to translation function', () => {
    mockExtractApiError.mockReturnValue({
      message: 'lockey_error_limit_exceeded',
      meta: { limit: '100' },
      errors: undefined,
    });

    const { result } = renderHook(() => useApiError());
    result.current.handleApiError(new Error('limit'));

    expect(mockT).toHaveBeenCalledWith('lockey_error_limit_exceeded', { limit: '100' });
  });

  it('should set field-level errors when validation errors exist and setError is provided', () => {
    mockExtractApiError.mockReturnValue({
      message: 'lockey_error_validation_failed',
      meta: undefined,
      errors: [
        { key: 'lockey_validation_email_invalid', params: { field: 'email' } },
        { key: 'lockey_validation_name_required', params: undefined },
      ],
    });

    const mockSetError = vi.fn();
    const { result } = renderHook(() => useApiError());
    result.current.handleApiError(new Error('validation'), mockSetError);

    expect(mockSetError).toHaveBeenCalledTimes(2);
    expect(mockSetError).toHaveBeenCalledWith('lockey_validation_email_invalid', {
      type: 'server',
      message: 'lockey_validation_email_invalid',
    });
    expect(mockSetError).toHaveBeenCalledWith('lockey_validation_name_required', {
      type: 'server',
      message: 'lockey_validation_name_required',
    });
    expect(mockToastError).not.toHaveBeenCalled();
  });

  it('should show toast when validation errors exist but no setError is provided', () => {
    mockExtractApiError.mockReturnValue({
      message: 'lockey_error_validation_failed',
      meta: undefined,
      errors: [{ key: 'lockey_validation_email_invalid' }],
    });

    const { result } = renderHook(() => useApiError());
    result.current.handleApiError(new Error('validation'));

    expect(mockToastError).toHaveBeenCalledWith('lockey_error_validation_failed');
  });

  it('should show toast when errors array is empty', () => {
    mockExtractApiError.mockReturnValue({
      message: 'lockey_error_something',
      meta: undefined,
      errors: [],
    });

    const { result } = renderHook(() => useApiError());
    result.current.handleApiError(new Error('empty errors'));

    expect(mockToastError).toHaveBeenCalledWith('lockey_error_something');
  });
});
