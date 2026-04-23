/**
 * URL safety helpers for attaching presigned or server-issued URLs to `<a href>`.
 *
 * The browser will happily execute `javascript:` / `data:` schemes when assigned
 * to an anchor and clicked programmatically — a stored XSS primitive. These
 * helpers are the single checkpoint used before any such assignment.
 */

/**
 * Accepts http(s) absolute URLs and same-origin relative paths; rejects
 * `javascript:`, `data:`, `blob:`, protocol-relative (`//evil.com/...`),
 * and anything else that could execute script on click.
 *
 * SSR-safe: when `window` is unavailable, the base origin falls back to a
 * placeholder so absolute URLs still parse deterministically.
 */
export function isSafeDownloadUrl(url: string): boolean {
  if (typeof url !== 'string' || url.length === 0) return false;
  if (url.startsWith('//')) return false;
  if (url.startsWith('/')) return true;

  const base =
    typeof globalThis !== 'undefined' &&
    typeof (globalThis as { location?: { origin?: string } }).location?.origin === 'string'
      ? (globalThis as { location: { origin: string } }).location.origin
      : 'https://nexora.invalid';

  try {
    const parsed = new URL(url, base);
    return parsed.protocol === 'https:' || parsed.protocol === 'http:';
  } catch {
    return false;
  }
}
