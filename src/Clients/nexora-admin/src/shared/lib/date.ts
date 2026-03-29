import i18n from '@/shared/lib/i18n';

/**
 * Formats a date string as relative time:
 * - Same day: "X hours ago" / "X minutes ago" / "Just now"
 * - Within last 30 days: "X days ago"
 * - Older: localized date string
 * - null/undefined: returns fallback text
 */
export function formatRelativeTime(
  dateStr: string | null | undefined,
  fallback: string = '-'
): string {
  if (!dateStr) return fallback;

  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
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
