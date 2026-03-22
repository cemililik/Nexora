import { describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';

// Mock react-i18next
let mockLanguage = 'en';
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    i18n: {
      get language() { return mockLanguage; },
    },
  }),
}));

// Mock i18n RTL_LOCALES
vi.mock('@/shared/lib/i18n', () => ({
  RTL_LOCALES: ['ar', 'he', 'fa', 'ur'],
}));

import { useDirection } from './useDirection';

describe('useDirection', () => {
  it('should return ltr for English', () => {
    mockLanguage = 'en';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('ltr');
  });

  it('should return ltr for Turkish', () => {
    mockLanguage = 'tr';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('ltr');
  });

  it('should return rtl for Arabic', () => {
    mockLanguage = 'ar';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('rtl');
  });

  it('should return rtl for Hebrew', () => {
    mockLanguage = 'he';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('rtl');
  });

  it('should handle locale with region subtag', () => {
    mockLanguage = 'ar-SA';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('rtl');
  });

  it('should return ltr for unknown locale', () => {
    mockLanguage = 'de';
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe('ltr');
  });
});
