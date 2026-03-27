import { describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';

// Mock next-intl
let mockLocale = 'en';
vi.mock('next-intl', () => ({
  useLocale: () => mockLocale,
}));

import { useDirection } from './useDirection';

describe('useDirection', () => {
  it('should return ltr for English', () => {
    mockLocale = 'en';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('ltr');
  });

  it('should return ltr for Turkish', () => {
    mockLocale = 'tr';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('ltr');
  });

  it('should return rtl for Arabic', () => {
    mockLocale = 'ar';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('rtl');
  });

  it('should return rtl for Hebrew', () => {
    mockLocale = 'he';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('rtl');
  });

  it('should return rtl for Farsi', () => {
    mockLocale = 'fa';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('rtl');
  });

  it('should return rtl for Urdu', () => {
    mockLocale = 'ur';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('rtl');
  });

  it('should handle locale with region subtag', () => {
    mockLocale = 'ar-SA';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('rtl');
  });

  it('should return ltr for unknown locale', () => {
    mockLocale = 'de';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('ltr');
  });
});
