import { describe, expect, it, vi } from 'vitest';

vi.mock('@/shared/lib/i18n', () => ({
  default: { language: 'en-US' },
}));

import { formatNumber, formatPercent } from './number';

describe('formatNumber', () => {
  it('formats an integer with en-US locale', () => {
    expect(formatNumber(1234567, 'en-US')).toBe('1,234,567');
  });

  it('formats a decimal with en-US locale', () => {
    expect(formatNumber(1234.56, 'en-US')).toBe('1,234.56');
  });

  it('formats a number with tr-TR locale using period thousands and comma decimal', () => {
    const result = formatNumber(1234.56, 'tr-TR');
    // tr-TR: "1.234,56"
    expect(result).toContain('1.234');
    expect(result).toContain(',56');
  });

  it('falls back to i18n.language when no locale is provided', () => {
    // i18n mock returns 'en-US', so should use comma thousands separator
    const result = formatNumber(1000);
    expect(result).toBe('1,000');
  });

  it('applies custom Intl.NumberFormatOptions', () => {
    const result = formatNumber(1234.5678, 'en-US', { maximumFractionDigits: 2 });
    expect(result).toBe('1,234.57');
  });

  it('formats zero correctly', () => {
    expect(formatNumber(0, 'en-US')).toBe('0');
  });

  it('formats negative numbers', () => {
    expect(formatNumber(-500, 'en-US')).toBe('-500');
  });
});

describe('formatPercent', () => {
  it('formats a percentage value with default 1 decimal digit', () => {
    const result = formatPercent(45.5, 'en-US');
    expect(result).toBe('45.5%');
  });

  it('formats a whole percentage value', () => {
    const result = formatPercent(100, 'en-US');
    expect(result).toBe('100%');
  });

  it('formats zero percent', () => {
    expect(formatPercent(0, 'en-US')).toBe('0%');
  });

  it('respects custom digits parameter', () => {
    const result = formatPercent(33.333, 'en-US', 2);
    expect(result).toBe('33.33%');
  });

  it('rounds to max fraction digits', () => {
    const result = formatPercent(66.666, 'en-US', 1);
    expect(result).toBe('66.7%');
  });

  it('falls back to i18n.language when no locale is provided', () => {
    const result = formatPercent(50);
    expect(result).toContain('50');
    expect(result).toContain('%');
  });

  it('formats with tr-TR locale', () => {
    const result = formatPercent(45.5, 'tr-TR');
    expect(result).toContain('45');
    expect(result).toContain('%');
  });
});
