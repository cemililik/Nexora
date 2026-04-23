import i18n from '@/shared/lib/i18n';

/**
 * Formats a date string as a locale-aware absolute date (no time).
 * Uses the tenant's IETF locale (e.g. "tr-TR") for correct regional formatting.
 *
 * @param dateStr - ISO date string, null, or undefined
 * @param locale  - IETF locale tag (e.g. "en-US", "tr-TR"). Defaults to i18n.language.
 * @param options - Intl.DateTimeFormatOptions overrides
 * @returns Formatted date string, or fallback "-" for null/invalid input
 */
export function formatDate(
  dateStr: string | null | undefined,
  locale?: string,
  options?: Intl.DateTimeFormatOptions,
  fallback = '-',
): string {
  if (!dateStr) return fallback;
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return fallback;
  return new Intl.DateTimeFormat(locale ?? i18n.language, options).format(date);
}

/**
 * Formats a date string as a locale-aware date + time string.
 * Uses the tenant's IETF locale (e.g. "tr-TR") for correct regional formatting.
 *
 * @param dateStr - ISO date string, null, or undefined
 * @param locale  - IETF locale tag (e.g. "en-US", "tr-TR"). Defaults to i18n.language.
 * @returns Formatted datetime string, or fallback "-" for null/invalid input
 */
export function formatDateTime(
  dateStr: string | null | undefined,
  locale?: string,
  fallback = '-',
): string {
  return formatDate(
    dateStr,
    locale,
    { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' },
    fallback,
  );
}

/**
 * Formats a date string as relative time:
 * - Future date: localized absolute date string
 * - Less than 1 minute ago: "Just now"
 * - Less than 1 hour ago: "X minutes ago"
 * - Less than 24 hours ago: "X hours ago"
 * - Within last 30 days: "X days ago"
 * - Older than 30 days: localized absolute date string
 * - null/undefined/invalid: returns fallback text (default: "-")
 *
 * @param locale - IETF locale tag for absolute date fallback. Defaults to i18n.language.
 */
export function formatRelativeTime(
  dateStr: string | null | undefined,
  fallback: string = '-',
  locale?: string,
): string {
  if (!dateStr) return fallback;

  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return fallback;
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const effectiveLocale = locale ?? i18n.language;

  if (diffMs < 0) return date.toLocaleDateString(effectiveLocale);

  const diffMinutes = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  const t = i18n.t;

  if (diffMinutes < 1) return t('lockey_common_just_now');
  if (diffMinutes < 60) return t('lockey_common_minutes_ago', { count: diffMinutes });
  if (diffHours < 24) return t('lockey_common_hours_ago', { count: diffHours });
  if (diffDays <= 30) return t('lockey_common_days_ago', { count: diffDays });

  return date.toLocaleDateString(effectiveLocale);
}
