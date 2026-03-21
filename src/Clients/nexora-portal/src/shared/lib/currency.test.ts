import { describe, expect, it } from 'vitest';

import { formatMoney, getCurrencySymbol } from './currency';

describe('formatMoney', () => {
  it('should format USD with default locale', () => {
    const result = formatMoney({ amount: 1234.56, currency: 'USD' });
    expect(result).toBe('$1,234.56');
  });

  it('should format TRY with Turkish locale', () => {
    const result = formatMoney({ amount: 1500, currency: 'TRY', locale: 'tr' });
    expect(result).toContain('1.500,00');
  });

  it('should format EUR with English locale', () => {
    const result = formatMoney({ amount: 99.9, currency: 'EUR' });
    expect(result).toContain('99.90');
  });

  it('should handle zero amount', () => {
    const result = formatMoney({ amount: 0, currency: 'USD' });
    expect(result).toBe('$0.00');
  });

  it('should handle negative amounts', () => {
    const result = formatMoney({ amount: -50, currency: 'USD' });
    expect(result).toContain('50.00');
  });

  it('should pad to two decimal places', () => {
    const result = formatMoney({ amount: 10, currency: 'USD' });
    expect(result).toBe('$10.00');
  });
});

describe('getCurrencySymbol', () => {
  it('should return $ for USD', () => {
    const symbol = getCurrencySymbol('USD');
    expect(symbol).toBe('$');
  });

  it('should return € for EUR', () => {
    const symbol = getCurrencySymbol('EUR');
    expect(symbol).toBe('€');
  });

  it('should return ₺ for TRY with Turkish locale', () => {
    const symbol = getCurrencySymbol('TRY', 'tr');
    expect(symbol).toBe('₺');
  });
});
