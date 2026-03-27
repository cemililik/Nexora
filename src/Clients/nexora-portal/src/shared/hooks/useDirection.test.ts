import { describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';

// Mock next-intl
let mockLocale = 'en';
vi.mock('next-intl', () => ({
  useLocale: () => mockLocale,
}));

import { useDirection } from './useDirection';

describe('useDirection', () => {
  it.each([
    ['en', 'ltr'],
    ['tr', 'ltr'],
    ['de', 'ltr'],
    ['ar', 'rtl'],
    ['he', 'rtl'],
    ['fa', 'rtl'],
    ['ur', 'rtl'],
    ['ar-SA', 'rtl'],
  ])('useDirection_WhenLocaleIs%s_Returns%s', (locale, expected) => {
    mockLocale = locale;
    const { result } = renderHook(() => useDirection());
    expect(result.current).toBe(expected);
  });
});
