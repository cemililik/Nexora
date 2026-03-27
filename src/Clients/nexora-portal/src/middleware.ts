import { type NextRequest, NextResponse } from 'next/server';
import { getToken } from 'next-auth/jwt';
import createIntlMiddleware from 'next-intl/middleware';

import { routing } from './i18n/routing';

const locales: readonly string[] = routing.locales;

const intlMiddleware = createIntlMiddleware(routing);

const publicPaths = ['/auth/login', '/auth/callback'];

function isPublicPath(pathname: string): boolean {
  // Strip locale prefix to get the route path
  const segments = pathname.split('/');
  const firstSegment = segments[1] ?? '';
  const pathWithoutLocale = locales.includes(firstSegment)
    ? '/' + segments.slice(2).join('/')
    : pathname;

  return publicPaths.some(
    (p) => pathWithoutLocale === p || pathWithoutLocale.startsWith(p + '/'),
  );
}

function extractLocale(pathname: string): string {
  const segments = pathname.split('/');
  const potentialLocale = segments[1] ?? '';
  return locales.includes(potentialLocale)
    ? potentialLocale
    : routing.defaultLocale;
}

/**
 * Middleware combining authentication (NextAuth v5) and internationalization (next-intl).
 *
 * Uses getToken() from next-auth/jwt to check auth state directly from the
 * session cookie. This avoids the auth() wrapper pattern which introduces a
 * NextAuthRequest ↔ NextRequest type mismatch with next-intl's middleware.
 *
 * See: https://next-intl.dev/docs/routing/middleware
 * See: https://authjs.dev/reference/nextjs#in-middleware
 */
export async function middleware(request: NextRequest): Promise<NextResponse> {
  const { pathname } = request.nextUrl;

  // Public paths: skip auth check, just handle i18n
  if (isPublicPath(pathname)) {
    return intlMiddleware(request);
  }

  // Check auth via JWT token from session cookie.
  // getToken() uses AUTH_SECRET env var to decode the encrypted JWT.
  const authSecret = process.env.AUTH_SECRET;
  if (!authSecret) {
    if (process.env.NODE_ENV !== 'production') console.error('[middleware] AUTH_SECRET is not set');
    return NextResponse.json({ error: 'Server configuration error' }, { status: 500 });
  }
  const token = await getToken({ req: request, secret: authSecret });

  if (!token || token.error === 'RefreshAccessTokenError') {
    const locale = extractLocale(pathname);
    const loginUrl = new URL(`/${locale}/auth/login`, request.url);
    return NextResponse.redirect(loginUrl);
  }

  // Authenticated: handle i18n routing
  return intlMiddleware(request);
}

export const config = {
  matcher: [
    // Match all pathnames except api, _next, and static files
    '/((?!api|_next|.*\\..*).*)',
  ],
};
