import i18n from '@/shared/lib/i18n';

/**
 * Formats a number using the tenant's IETF locale (e.g. "tr-TR").
 * Uses correct decimal/grouping separators for the region
 * (e.g. "1,234.56" for en-US, "1.234,56" for tr-TR).
 *
 * @param value   - Numeric value to format
 * @param locale  - IETF locale tag. Defaults to i18n.language.
 * @param options - Intl.NumberFormatOptions overrides
 */
export function formatNumber(
  value: number,
  locale?: string,
  options?: Intl.NumberFormatOptions,
): string {
  return new Intl.NumberFormat(locale ?? i18n.language, options).format(value);
}

/**
 * Formats a number as a percentage.
 * @param value   - Value between 0 and 100 (e.g. 45.5 → "45.5%")
 * @param locale  - IETF locale tag. Defaults to i18n.language.
 * @param digits  - Maximum fraction digits (default: 1)
 */
export function formatPercent(
  value: number,
  locale?: string,
  digits = 1,
): string {
  return new Intl.NumberFormat(locale ?? i18n.language, {
    style: 'percent',
    maximumFractionDigits: digits,
  }).format(value / 100);
}
