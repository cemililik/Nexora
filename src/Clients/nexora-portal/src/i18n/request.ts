import { getRequestConfig } from 'next-intl/server';

import { routing } from './routing';

export default getRequestConfig(async ({ requestLocale }) => {
  let locale = await requestLocale;

  if (!locale || !routing.locales.includes(locale as 'en' | 'tr')) {
    locale = routing.defaultLocale;
  }

  const [common, error, validation, navigation] = await Promise.all([
    import(`@/locales/${locale}/common.json`),
    import(`@/locales/${locale}/error.json`),
    import(`@/locales/${locale}/validation.json`),
    import(`@/locales/${locale}/navigation.json`),
  ]);

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
