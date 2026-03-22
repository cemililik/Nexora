export interface FormatMoneyOptions {
  amount: number;
  currency: string;
  locale?: string;
  minimumFractionDigits?: number;
  maximumFractionDigits?: number;
}

/**
 * Format a monetary amount using the browser's Intl.NumberFormat.
 * Mirrors backend Money value object (amount + 3-char ISO currency code).
 */
export function formatMoney({
  amount,
  currency,
  locale = 'en',
  minimumFractionDigits,
  maximumFractionDigits,
}: FormatMoneyOptions): string {
  const options: Intl.NumberFormatOptions = {
    style: 'currency',
    currency,
  };
  if (minimumFractionDigits !== undefined) {
    options.minimumFractionDigits = minimumFractionDigits;
  }
  if (maximumFractionDigits !== undefined) {
    options.maximumFractionDigits = maximumFractionDigits;
  }
  return new Intl.NumberFormat(locale, options).format(amount);
}

/**
 * Parse a currency code to its symbol for compact display.
 * Falls back to the currency code itself if no symbol is found.
 */
export function getCurrencySymbol(
  currency: string,
  locale = 'en',
): string {
  let parts: Intl.NumberFormatPart[];
  try {
    parts = new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      currencyDisplay: 'narrowSymbol',
    }).formatToParts(0);
  } catch {
    parts = new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      currencyDisplay: 'symbol',
    }).formatToParts(0);
  }

  return parts.find((p) => p.type === 'currency')?.value ?? currency;
}
