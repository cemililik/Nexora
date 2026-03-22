import { useTranslation } from 'react-i18next';

const RTL_LOCALES = new Set(['ar', 'he', 'fa', 'ur']);

/** Returns 'rtl' or 'ltr' based on the current language. */
export function useDirection(): 'ltr' | 'rtl' {
  const { i18n } = useTranslation();
  const baseLocale = i18n.language.split('-')[0] ?? i18n.language;
  return RTL_LOCALES.has(baseLocale) ? 'rtl' : 'ltr';
}
