import { describe, expect, it, vi, afterEach } from 'vitest';

vi.mock('@/shared/lib/i18n', () => ({
  default: {
    t: (key: string, params?: Record<string, unknown>) => {
      if (params && 'count' in params) {
        return `${key}:${params.count}`;
      }
      return key;
    },
    language: 'en',
  },
}));

import { formatDate, formatDateTime, formatRelativeTime } from './date';

describe('formatRelativeTime', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('should return just now for timestamps less than a minute ago', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-03-29T12:00:30Z'));

    const result = formatRelativeTime('2026-03-29T12:00:00Z');

    expect(result).toBe('lockey_common_just_now');
  });

  it('should return minutes ago for timestamps within the last hour', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-03-29T12:15:00Z'));

    const result = formatRelativeTime('2026-03-29T12:00:00Z');

    expect(result).toBe('lockey_common_minutes_ago:15');
  });

  it('should return hours ago for timestamps within the last day', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-03-29T15:00:00Z'));

    const result = formatRelativeTime('2026-03-29T12:00:00Z');

    expect(result).toBe('lockey_common_hours_ago:3');
  });

  it('should return days ago for timestamps within the last 30 days', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-03-29T12:00:00Z'));

    const result = formatRelativeTime('2026-03-24T12:00:00Z');

    expect(result).toBe('lockey_common_days_ago:5');
  });

  it('should return localized date for timestamps older than 30 days', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-03-29T12:00:00Z'));

    const result = formatRelativeTime('2026-01-15T12:00:00Z');

    // Should be a localized date string matching the expected date
    expect(result).toBe(new Date('2026-01-15T12:00:00Z').toLocaleDateString('en'));
  });

  it('should return fallback for null input', () => {
    const result = formatRelativeTime(null);

    expect(result).toBe('-');
  });

  it('should return fallback for undefined input', () => {
    const result = formatRelativeTime(undefined);

    expect(result).toBe('-');
  });

  it('should return custom fallback when provided', () => {
    const result = formatRelativeTime(null, 'N/A');

    expect(result).toBe('N/A');
  });

  it('should return fallback for empty string', () => {
    const result = formatRelativeTime('');

    expect(result).toBe('-');
  });

  it('should return fallback for invalid date string', () => {
    const result = formatRelativeTime('not-a-date');

    expect(result).toBe('-');
  });

  it('should return localized date for future date', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-03-29T12:00:00Z'));

    const result = formatRelativeTime('2026-04-15T12:00:00Z');

    expect(result).toBe(new Date('2026-04-15T12:00:00Z').toLocaleDateString('en'));
  });
});

describe('formatDate', () => {
  it('should return fallback for null input', () => {
    expect(formatDate(null)).toBe('-');
  });

  it('should return fallback for undefined input', () => {
    expect(formatDate(undefined)).toBe('-');
  });

  it('should return fallback for empty string', () => {
    expect(formatDate('')).toBe('-');
  });

  it('should return custom fallback when provided', () => {
    expect(formatDate(null, undefined, undefined, 'N/A')).toBe('N/A');
  });

  it('should return fallback for invalid date string', () => {
    expect(formatDate('not-a-date')).toBe('-');
  });

  it('should format a valid ISO date with en-US locale', () => {
    const result = formatDate('2024-03-15', 'en-US');
    // en-US formats as M/D/YYYY
    expect(result).toMatch(/3\/15\/2024/);
  });

  it('should format a valid ISO date with tr-TR locale', () => {
    const result = formatDate('2024-03-15', 'tr-TR');
    // tr-TR formats as D.M.YYYY
    expect(result).toMatch(/15\.03\.2024/);
  });

  it('should apply custom Intl.DateTimeFormatOptions', () => {
    const result = formatDate('2024-03-15', 'en-US', { month: 'long', year: 'numeric' });
    expect(result).toContain('March');
    expect(result).toContain('2024');
  });
});

describe('formatDateTime', () => {
  it('should return fallback for null input', () => {
    expect(formatDateTime(null)).toBe('-');
  });

  it('should return fallback for undefined input', () => {
    expect(formatDateTime(undefined)).toBe('-');
  });

  it('should return custom fallback when provided', () => {
    expect(formatDateTime(null, undefined, 'n/a')).toBe('n/a');
  });

  it('should format date and time components', () => {
    // Use a fixed UTC timestamp and en-US locale for deterministic output
    const result = formatDateTime('2024-03-15T14:30:00Z', 'en-US');
    expect(result).toContain('2024');
    expect(result).toMatch(/\d{2}:\d{2}/);
  });

  it('should return fallback for invalid date string', () => {
    expect(formatDateTime('bad-date')).toBe('-');
  });
});
