import { getRequestConfig } from 'next-intl/server';

import { routing } from './routing';

type Locale = (typeof routing.locales)[number];

export default getRequestConfig(async ({ requestLocale }) => {
  let locale = await requestLocale;

  if (!locale || !routing.locales.includes(locale as Locale)) {
    locale = routing.defaultLocale;
  }

  const [common, error, validation, navigation] = await Promise.all([
    import(`@/locales/${locale}/common.json`),
    import(`@/locales/${locale}/error.json`),
    import(`@/locales/${locale}/validation.json`),
    import(`@/locales/${locale}/navigation.json`),
  ]);

  // Namespace-keyed messages — each JSON file is a separate namespace.
  // Usage: const t = useTranslations('common'); t('lockey_common_save')
  // For cross-namespace access: const te = useTranslations('error'); te('lockey_error_...')
  return {
    locale,
    messages: {
      common: common.default,
      error: error.default,
      validation: validation.default,
      navigation: navigation.default,
    },
  };
});
