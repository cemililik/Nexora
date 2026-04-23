/** BCP 47 locales supported by the platform. */
export const SUPPORTED_LOCALES = ['en-US', 'tr-TR'] as const;
export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number];

/** ISO 4217 currencies supported by the platform. */
export const SUPPORTED_CURRENCIES = ['USD', 'EUR', 'TRY'] as const;
export type SupportedCurrency = (typeof SUPPORTED_CURRENCIES)[number];

/** IANA timezone identifiers supported by the platform. */
export const SUPPORTED_TIMEZONES = [
  'UTC',
  'Europe/Istanbul',
  'Europe/London',
  'Europe/Berlin',
  'America/New_York',
  'America/Chicago',
  'America/Denver',
  'America/Los_Angeles',
  'Asia/Dubai',
] as const;
export type SupportedTimezone = (typeof SUPPORTED_TIMEZONES)[number];

/** BCP 47 language tags supported for UI and document rendering. */
export const SUPPORTED_LANGUAGES = ['en', 'tr'] as const;
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];
