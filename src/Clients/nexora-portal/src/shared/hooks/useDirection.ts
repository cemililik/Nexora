'use client';

import { useLocale } from 'next-intl';

const RTL_LOCALES = new Set(['ar', 'he', 'fa', 'ur']);

/**
 * Returns the text direction ('ltr' or 'rtl') based on the current locale.
 * Normalizes locale to base language (e.g., 'ar-SA' → 'ar') for RTL check.
 */
export function useDirection(): 'ltr' | 'rtl' {
  const locale = useLocale();
  const baseLocale = locale.split('-')[0] ?? locale;
  return RTL_LOCALES.has(baseLocale) ? 'rtl' : 'ltr';
}
