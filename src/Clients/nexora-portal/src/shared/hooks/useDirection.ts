'use client';

import { useLocale } from 'next-intl';

const RTL_LOCALES = new Set(['ar', 'he', 'fa', 'ur']);

/**
 * Returns the text direction ('ltr' or 'rtl') based on the current locale.
 * Future-proofed for Arabic locale support.
 */
export function useDirection(): 'ltr' | 'rtl' {
  const locale = useLocale();
  return RTL_LOCALES.has(locale) ? 'rtl' : 'ltr';
}
