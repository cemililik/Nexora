import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';

import { setApiLanguage } from '@/shared/lib/api';
import enCommon from '@/locales/en/common.json';
import enError from '@/locales/en/error.json';
import enNavigation from '@/locales/en/navigation.json';
import enValidation from '@/locales/en/validation.json';
import trCommon from '@/locales/tr/common.json';
import trError from '@/locales/tr/error.json';
import trNavigation from '@/locales/tr/navigation.json';
import trValidation from '@/locales/tr/validation.json';

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      en: {
        common: enCommon,
        error: enError,
        navigation: enNavigation,
        validation: enValidation,
      },
      tr: {
        common: trCommon,
        error: trError,
        navigation: trNavigation,
        validation: trValidation,
      },
    },
    defaultNS: 'common',
    fallbackLng: 'en',
    supportedLngs: ['en', 'tr'],
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
    },
  });

/**
 * Register locale resources for a feature module at runtime.
 * Call this from each module's init/manifest file to decouple
 * module translations from the core i18n setup.
 *
 * @param moduleName - Namespace for the module (e.g., 'identity')
 * @param locales - Map of language codes to translation objects (e.g., { en: {...}, tr: {...} })
 */
export function registerModuleLocales(
  moduleName: string,
  locales: Record<string, Record<string, string>>,
): void {
  for (const [lang, translations] of Object.entries(locales)) {
    i18n.addResourceBundle(lang, moduleName, translations, true, true);
  }
}

const RTL_LOCALES = ['ar', 'he', 'fa'];

// Sync API client Accept-Language header with current and future language changes
i18n.on('initialized', () => {
  setApiLanguage(i18n.language || 'en');
});
i18n.on('languageChanged', (lang: string) => {
  setApiLanguage(lang);
  if (typeof document !== 'undefined') {
    document.documentElement.lang = lang;
    document.documentElement.dir = RTL_LOCALES.includes(lang) ? 'rtl' : 'ltr';
  }
});

export default i18n;
