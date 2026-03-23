import { useTranslation } from 'react-i18next';

import { RTL_LOCALES } from '@/shared/lib/i18n';

/** Returns 'rtl' or 'ltr' based on the current language. */
export function useDirection(): 'ltr' | 'rtl' {
  const { i18n } = useTranslation();
  const baseLocale = i18n.language.split('-')[0] ?? i18n.language;
  return RTL_LOCALES.includes(baseLocale) ? 'rtl' : 'ltr';
}
