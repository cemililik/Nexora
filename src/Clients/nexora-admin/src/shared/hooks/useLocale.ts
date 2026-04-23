import { useAuthStore } from '@/shared/lib/stores/authStore';
import { formatDate, formatDateTime, formatRelativeTime } from '@/shared/lib/date';
import { formatNumber, formatPercent } from '@/shared/lib/number';

/**
 * Returns the current tenant's locale settings and locale-bound formatting helpers.
 *
 * All formatting functions automatically use the tenant's IETF locale
 * (e.g. "tr-TR") so call sites don't need to thread locale through manually.
 *
 * Falls back to i18n.language / platform defaults when tenantLocale is not yet loaded.
 */
export function useLocale() {
  const tenantLocale = useAuthStore((s) => s.tenantLocale);

  const locale = tenantLocale?.locale ?? undefined;
  const currency = tenantLocale?.currency ?? 'USD';
  const timezone = tenantLocale?.timezone ?? 'UTC';
  const documentLanguage = tenantLocale?.documentLanguage ?? 'en';

  return {
    /** IETF locale tag (e.g. "en-US", "tr-TR"). Undefined until tenant loads. */
    locale,
    /** ISO 4217 currency code (e.g. "USD", "TRY"). */
    currency,
    /** IANA timezone identifier (e.g. "UTC", "Europe/Istanbul"). */
    timezone,
    /** BCP 47 language for document rendering (e.g. "en", "tr"). */
    documentLanguage,

    /** Format a date string using the tenant locale. */
    formatDate: (dateStr: string | null | undefined, options?: Intl.DateTimeFormatOptions) =>
      formatDate(dateStr, locale, options),

    /** Format a date + time string using the tenant locale. */
    formatDateTime: (dateStr: string | null | undefined) =>
      formatDateTime(dateStr, locale),

    /** Format a relative time string (e.g. "3 days ago") using the tenant locale for absolute fallback. */
    formatRelativeTime: (dateStr: string | null | undefined, fallback?: string) =>
      formatRelativeTime(dateStr, fallback, locale),

    /** Format a plain number using the tenant locale. */
    formatNumber: (value: number, options?: Intl.NumberFormatOptions) =>
      formatNumber(value, locale, options),

    /** Format a number as currency using the tenant locale and currency code. */
    formatCurrency: (value: number) =>
      formatNumber(value, locale, { style: 'currency', currency }),

    /** Format a number as percentage (0–100) using the tenant locale. */
    formatPercent: (value: number, digits?: number) =>
      formatPercent(value, locale, digits),
  };
}
