export interface FormatMoneyOptions {
  amount: number;
  currency: string;
  locale?: string;
}

/**
 * Format a monetary amount using the browser's Intl.NumberFormat.
 * Mirrors backend Money value object (amount + 3-char ISO currency code).
 */
export function formatMoney({
  amount,
  currency,
  locale = 'en',
}: FormatMoneyOptions): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount);
}

/**
 * Parse a currency code to its symbol for compact display.
 * Falls back to the currency code itself if no symbol is found.
 */
export function getCurrencySymbol(
  currency: string,
  locale = 'en',
): string {
  const parts = new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
    currencyDisplay: 'narrowSymbol',
  }).formatToParts(0);

  return parts.find((p) => p.type === 'currency')?.value ?? currency;
}
