import { describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';

// Mock next-intl
let mockLocale = 'en';
vi.mock('next-intl', () => ({
  useLocale: () => mockLocale,
}));

// Mock useOrganization
const mockOrganization = vi.fn();
vi.mock('./useOrganization', () => ({
  useOrganization: () => ({ organization: mockOrganization() }),
}));

// Mock formatMoney from currency lib
vi.mock('@/shared/lib/currency', () => ({
  formatMoney: ({ amount, currency, locale }: { amount: number; currency: string; locale: string }) =>
    new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount),
}));

import { useCurrency } from './useCurrency';

describe('useCurrency', () => {
  it('should use organization default currency', () => {
    mockLocale = 'en';
    mockOrganization.mockReturnValue({ defaultCurrency: 'EUR' });

    const { result } = renderHook(() => useCurrency());

    expect(result.current.defaultCurrency).toBe('EUR');
  });

  it('should fallback to USD when no organization', () => {
    mockLocale = 'en';
    mockOrganization.mockReturnValue(null);

    const { result } = renderHook(() => useCurrency());

    expect(result.current.defaultCurrency).toBe('USD');
  });

  it('should format USD amount with English locale', () => {
    mockLocale = 'en';
    mockOrganization.mockReturnValue({ defaultCurrency: 'USD' });

    const { result } = renderHook(() => useCurrency());

    const formatted = result.current.format(1234.56);
    expect(formatted).toMatch(/\$\s?1[,.]234[.,]56/);
  });

  it('should format with overridden currency', () => {
    mockLocale = 'en';
    mockOrganization.mockReturnValue({ defaultCurrency: 'USD' });

    const { result } = renderHook(() => useCurrency());

    const formatted = result.current.format(1500, 'EUR');
    expect(formatted).toMatch(/1[,.]500[.,]00/);
  });

  it('should format TRY amount with Turkish locale', () => {
    mockLocale = 'tr';
    mockOrganization.mockReturnValue({ defaultCurrency: 'TRY' });

    const { result } = renderHook(() => useCurrency());

    const formatted = result.current.format(1500);
    expect(formatted).toMatch(/1[.,]500[.,]00/);
  });

  it('should handle zero amount', () => {
    mockLocale = 'en';
    mockOrganization.mockReturnValue({ defaultCurrency: 'USD' });

    const { result } = renderHook(() => useCurrency());

    const formatted = result.current.format(0);
    expect(formatted).toMatch(/\$\s?0[.,]00/);
  });
});
