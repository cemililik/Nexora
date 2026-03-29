import i18n from '@/shared/lib/i18n';

/**
 * Formats a date string as relative time:
 * - Future date: localized absolute date string
 * - Less than 1 minute ago: "Just now"
 * - Less than 1 hour ago: "X minutes ago"
 * - Less than 24 hours ago: "X hours ago"
 * - Within last 30 days: "X days ago"
 * - Older than 30 days: localized absolute date string
 * - null/undefined/invalid: returns fallback text (default: "-")
 */
export function formatRelativeTime(
  dateStr: string | null | undefined,
  fallback: string = '-'
): string {
  if (!dateStr) return fallback;

  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return fallback;
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();

  if (diffMs < 0) return date.toLocaleDateString(i18n.language);

  const diffMinutes = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  const t = i18n.t;

  if (diffMinutes < 1) return t('lockey_common_just_now');
  if (diffMinutes < 60) return t('lockey_common_minutes_ago', { count: diffMinutes });
  if (diffHours < 24) return t('lockey_common_hours_ago', { count: diffHours });
  if (diffDays <= 30) return t('lockey_common_days_ago', { count: diffDays });

  return date.toLocaleDateString(i18n.language);
}
