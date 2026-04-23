import { describe, expect, it } from 'vitest';

import { isSafeDownloadUrl } from './urlSafety';

describe('isSafeDownloadUrl', () => {
  it('accepts absolute https urls', () => {
    expect(isSafeDownloadUrl('https://minio.example.com/bucket/file.csv')).toBe(true);
  });

  it('accepts absolute http urls', () => {
    expect(isSafeDownloadUrl('http://minio.local/bucket/file.csv')).toBe(true);
  });

  it('accepts same-origin relative paths', () => {
    expect(isSafeDownloadUrl('/api/v1/contacts/export/123/download')).toBe(true);
  });

  it('rejects javascript: urls', () => {
    expect(isSafeDownloadUrl('javascript:alert(1)')).toBe(false);
  });

  it('rejects data: urls', () => {
    expect(isSafeDownloadUrl('data:text/html,<script>alert(1)</script>')).toBe(false);
  });

  it('rejects blob: urls', () => {
    expect(isSafeDownloadUrl('blob:https://evil.com/uuid')).toBe(false);
  });

  it('rejects protocol-relative urls', () => {
    expect(isSafeDownloadUrl('//evil.com/steal')).toBe(false);
  });

  it('rejects empty strings', () => {
    expect(isSafeDownloadUrl('')).toBe(false);
  });

  it('rejects file: urls', () => {
    expect(isSafeDownloadUrl('file:///etc/passwd')).toBe(false);
  });
});
