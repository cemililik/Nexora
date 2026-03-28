import { beforeEach, describe, expect, it, vi } from 'vitest';
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
  beforeEach(() => {
    mockLocale = 'en';
    mockOrganization.mockReset();
    mockOrganization.mockReturnValue(null);
  });

  it('defaultCurrency_WithOrganizationCurrency_ReturnsOrganizationCurrency', () => {
    mockOrganization.mockReturnValue({ defaultCurrency: 'EUR' });

    const { result } = renderHook(() => useCurrency());

    expect(result.current.defaultCurrency).toBe('EUR');
  });

  it('defaultCurrency_WithNoOrganization_FallsBackToUSD', () => {
    const { result } = renderHook(() => useCurrency());

    expect(result.current.defaultCurrency).toBe('USD');
  });

  it('format_USDWithEnglishLocale_FormatsCorrectly', () => {
    mockOrganization.mockReturnValue({ defaultCurrency: 'USD' });

    const { result } = renderHook(() => useCurrency());

    const formatted = result.current.format(1234.56);
    expect(formatted).toMatch(/\$\s?1[,.]234[.,]56/);
  });

  it('format_WithOverriddenCurrency_UsesOverrideCurrency', () => {
    mockOrganization.mockReturnValue({ defaultCurrency: 'USD' });

    const { result } = renderHook(() => useCurrency());

    const formatted = result.current.format(1500, 'EUR');
    expect(formatted).toMatch(/1[,.]500[.,]00/);
    expect(formatted).toMatch(/€|EUR/);
  });

  it('format_TRYWithTurkishLocale_FormatsCorrectly', () => {
    mockLocale = 'tr';
    mockOrganization.mockReturnValue({ defaultCurrency: 'TRY' });

    const { result } = renderHook(() => useCurrency());

    const formatted = result.current.format(1500);
    expect(formatted).toMatch(/1[.,]500[.,]00/);
    expect(formatted).toMatch(/₺|TRY/);
  });

  it('format_ZeroAmount_FormatsCorrectly', () => {
    mockOrganization.mockReturnValue({ defaultCurrency: 'USD' });

    const { result } = renderHook(() => useCurrency());

    const formatted = result.current.format(0);
    expect(formatted).toMatch(/\$\s?0[.,]00/);
  });
});
