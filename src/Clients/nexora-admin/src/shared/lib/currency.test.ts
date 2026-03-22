import { describe, expect, it } from 'vitest';

import { formatMoney, getCurrencySymbol } from './currency';

describe('formatMoney', () => {
  it('should format USD amount with default locale', () => {
    expect(formatMoney({ amount: 1234.5, currency: 'USD' })).toBe('$1,234.50');
  });

  it('should format EUR amount with tr locale', () => {
    const result = formatMoney({ amount: 1000, currency: 'EUR', locale: 'tr' });
    expect(result).toContain('1.000,00');
  });

  it('should format TRY amount with tr locale', () => {
    const result = formatMoney({ amount: 250.99, currency: 'TRY', locale: 'tr' });
    expect(result).toContain('250,99');
  });

  it('should always show 2 decimal places', () => {
    expect(formatMoney({ amount: 100, currency: 'USD' })).toBe('$100.00');
  });
});

describe('getCurrencySymbol', () => {
  it('should return $ for USD', () => {
    expect(getCurrencySymbol('USD')).toBe('$');
  });

  it('should return € for EUR', () => {
    expect(getCurrencySymbol('EUR')).toBe('€');
  });

  it('should return symbol for TRY with tr locale', () => {
    const symbol = getCurrencySymbol('TRY', 'tr');
    expect(symbol).toBe('₺');
  });
});
