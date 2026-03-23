import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';

// Mock useSearchParams
let mockSearchParams = new URLSearchParams();
const mockSetSearchParams = vi.fn();

vi.mock('react-router', () => ({
  useSearchParams: () => [mockSearchParams, mockSetSearchParams],
}));

import { usePagination } from './usePagination';

function getUpdaterFn(mock: ReturnType<typeof vi.fn>, callIndex: number): (prev: URLSearchParams) => URLSearchParams {
  const maybeUpdater = mock.mock.calls[callIndex]?.[0];
  expect(typeof maybeUpdater).toBe('function');
  return maybeUpdater as (prev: URLSearchParams) => URLSearchParams;
}

describe('usePagination', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSearchParams = new URLSearchParams();
  });

  it('should return default page 1 and pageSize 20 when no URL params', () => {
    const { result } = renderHook(() => usePagination());

    expect(result.current.page).toBe(1);
    expect(result.current.pageSize).toBe(20);
  });

  it('should use custom default pageSize when provided', () => {
    const { result } = renderHook(() => usePagination(50));

    expect(result.current.pageSize).toBe(50);
  });

  it('should read page and pageSize from URL params', () => {
    mockSearchParams = new URLSearchParams('page=3&pageSize=10');

    const { result } = renderHook(() => usePagination());

    expect(result.current.page).toBe(3);
    expect(result.current.pageSize).toBe(10);
  });

  it('should fall back to defaults for invalid URL params', () => {
    mockSearchParams = new URLSearchParams('page=-1&pageSize=abc');

    const { result } = renderHook(() => usePagination());

    expect(result.current.page).toBe(1);
    expect(result.current.pageSize).toBe(20);
  });

  it('should call setSearchParams with new page when setPage is called', () => {
    const { result } = renderHook(() => usePagination());

    act(() => {
      result.current.setPage(5);
    });

    expect(mockSetSearchParams).toHaveBeenCalledTimes(1);
    // Verify the updater function produces correct params
    const updaterFn = getUpdaterFn(mockSetSearchParams, 0);
    const newParams = updaterFn(new URLSearchParams());
    expect(newParams.get('page')).toBe('5');
  });

  it('should reset page to 1 when setPageSize is called', () => {
    const { result } = renderHook(() => usePagination());

    act(() => {
      result.current.setPageSize(50);
    });

    expect(mockSetSearchParams).toHaveBeenCalledTimes(1);
    const updaterFn = getUpdaterFn(mockSetSearchParams, 0);
    const newParams = updaterFn(new URLSearchParams('page=5'));
    expect(newParams.get('pageSize')).toBe('50');
    expect(newParams.get('page')).toBe('1');
  });
});
