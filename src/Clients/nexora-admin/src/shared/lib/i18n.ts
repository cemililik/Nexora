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
        validation: enValidation,
        navigation: enNavigation,
      },
      tr: {
        common: trCommon,
        error: trError,
        validation: trValidation,
        navigation: trNavigation,
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

// Sync API client Accept-Language header with current and future language changes
i18n.on('initialized', () => {
  setApiLanguage(i18n.language || 'en');
});
i18n.on('languageChanged', (lang: string) => {
  setApiLanguage(lang);
});

export default i18n;
