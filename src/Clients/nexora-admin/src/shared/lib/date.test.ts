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

import { formatRelativeTime } from './date';

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

    // Should be a localized date string, not a lockey_ key
    expect(result).not.toContain('lockey_');
    expect(result).toBeTruthy();
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
});
