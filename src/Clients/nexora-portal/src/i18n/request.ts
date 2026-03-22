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

  // Flat merge — all lockey_ keys use unique prefixes (lockey_common_*, lockey_error_*, etc.)
  // so key collisions are prevented by naming convention.
  // TODO Phase 2: Switch to namespace-keyed messages when module translations are added:
  //   messages: { common: common.default, error: error.default, ... }
  //   Usage: const t = useTranslations('common'); t('lockey_common_save')
  return {
    locale,
    messages: {
      ...common.default,
      ...error.default,
      ...validation.default,
      ...navigation.default,
    },
  };
});
