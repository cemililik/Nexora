'use client';

import { useCallback } from 'react';
import { useLocale } from 'next-intl';

import { formatMoney } from '@/shared/lib/currency';

import { useOrganization } from './useOrganization';

/**
 * Hook for formatting monetary values using the organization's
 * default currency and the current locale.
 */
export function useCurrency() {
  const locale = useLocale();
  const { organization } = useOrganization();
  const defaultCurrency = organization?.defaultCurrency ?? 'USD';

  const format = useCallback(
    (amount: number, currency?: string): string =>
      formatMoney({
        amount,
        currency: currency ?? defaultCurrency,
        locale,
      }),
    [defaultCurrency, locale],
  );

  return { format, defaultCurrency };
}
