import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';

vi.mock('@/shared/lib/i18n', () => ({
  default: {
    language: 'en-US',
    t: (key: string, params?: Record<string, unknown>) => {
      if (params && 'count' in params) return `${key}:${params.count}`;
      return key;
    },
  },
}));

import { useAuthStore } from '@/shared/lib/stores/authStore';
import { useLocale } from './useLocale';

const trLocale = {
  locale: 'tr-TR',
  currency: 'TRY',
  timezone: 'Europe/Istanbul',
  documentLanguage: 'tr',
};

describe('useLocale', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession();
  });

  describe('when tenantLocale is not loaded', () => {
    it('returns undefined locale', () => {
      const { result } = renderHook(() => useLocale());
      expect(result.current.locale).toBeUndefined();
    });

    it('returns USD as default currency', () => {
      const { result } = renderHook(() => useLocale());
      expect(result.current.currency).toBe('USD');
    });

    it('returns UTC as default timezone', () => {
      const { result } = renderHook(() => useLocale());
      expect(result.current.timezone).toBe('UTC');
    });

    it('returns en as default documentLanguage', () => {
      const { result } = renderHook(() => useLocale());
      expect(result.current.documentLanguage).toBe('en');
    });
  });

  describe('when tenantLocale is loaded', () => {
    beforeEach(() => {
      useAuthStore.getState().setTenantLocale(trLocale);
    });

    it('returns the tenant locale', () => {
      const { result } = renderHook(() => useLocale());
      expect(result.current.locale).toBe('tr-TR');
    });

    it('returns the tenant currency', () => {
      const { result } = renderHook(() => useLocale());
      expect(result.current.currency).toBe('TRY');
    });

    it('returns the tenant timezone', () => {
      const { result } = renderHook(() => useLocale());
      expect(result.current.timezone).toBe('Europe/Istanbul');
    });

    it('returns the tenant documentLanguage', () => {
      const { result } = renderHook(() => useLocale());
      expect(result.current.documentLanguage).toBe('tr');
    });
  });

  describe('formatDate', () => {
    it('returns fallback for null', () => {
      const { result } = renderHook(() => useLocale());
      expect(result.current.formatDate(null)).toBe('-');
    });

    it('formats date using tenant locale', () => {
      useAuthStore.getState().setTenantLocale(trLocale);
      const { result } = renderHook(() => useLocale());
      const formatted = result.current.formatDate('2024-03-15');
      // tr-TR format: 15.03.2024
      expect(formatted).toMatch(/15\.03\.2024/);
    });
  });

  describe('formatDateTime', () => {
    it('returns fallback for null', () => {
      const { result } = renderHook(() => useLocale());
      expect(result.current.formatDateTime(null)).toBe('-');
    });

    it('formats datetime using tenant locale', () => {
      useAuthStore.getState().setTenantLocale(trLocale);
      const { result } = renderHook(() => useLocale());
      const formatted = result.current.formatDateTime('2024-03-15T10:30:00Z');
      expect(formatted).toContain('2024');
      expect(formatted).toMatch(/\d{2}:\d{2}/);
    });
  });

  describe('formatNumber', () => {
    it('formats a number using tenant locale', () => {
      useAuthStore.getState().setTenantLocale(trLocale);
      const { result } = renderHook(() => useLocale());
      const formatted = result.current.formatNumber(1234.56);
      // tr-TR: "1.234,56"
      expect(formatted).toContain('1.234');
      expect(formatted).toContain(',56');
    });
  });

  describe('formatCurrency', () => {
    it('formats currency using tenant locale and currency code', () => {
      useAuthStore.getState().setTenantLocale(trLocale);
      const { result } = renderHook(() => useLocale());
      const formatted = result.current.formatCurrency(250);
      // Should use TRY currency code and tr-TR locale
      expect(formatted).toContain('250');
    });

    it('uses USD as default currency when tenantLocale is not loaded', () => {
      const { result } = renderHook(() => useLocale());
      const formatted = result.current.formatCurrency(100);
      expect(formatted).toContain('100');
    });
  });

  describe('formatPercent', () => {
    it('formats a percentage', () => {
      useAuthStore.getState().setTenantLocale(trLocale);
      const { result } = renderHook(() => useLocale());
      const formatted = result.current.formatPercent(75);
      expect(formatted).toContain('75');
      expect(formatted).toContain('%');
    });

    it('respects custom digits', () => {
      useAuthStore.getState().setTenantLocale({ ...trLocale, locale: 'en-US' });
      const { result } = renderHook(() => useLocale());
      const formatted = result.current.formatPercent(33.333, 2);
      expect(formatted).toBe('33.33%');
    });
  });
});
