import { type NextRequest, NextResponse } from 'next/server';
import createIntlMiddleware from 'next-intl/middleware';

import { auth } from '@/shared/lib/auth';
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

// NextAuth v5 auth() wrapper returns a compatible middleware function.
// The intlMiddleware expects NextRequest but auth callback provides NextAuthRequest.
// This is a known NextAuth v5 + next-intl interop issue — the cast is safe because
// NextAuthRequest extends NextRequest with an additional `auth` property.
export default auth((req) => {
  const isAuth = !!req.auth;
  const { pathname } = req.nextUrl;

  // Allow public paths without auth
  if (isPublicPath(pathname)) {
    return intlMiddleware(req as unknown as NextRequest);
  }

  // Redirect unauthenticated users to login
  if (!isAuth) {
    const locale = extractLocale(pathname);
    const loginUrl = new URL(`/${locale}/auth/login`, req.url);
    return NextResponse.redirect(loginUrl);
  }

  return intlMiddleware(req as unknown as NextRequest);
}) as unknown as (req: NextRequest) => Promise<NextResponse>;

export const config = {
  matcher: [
    // Match all pathnames except api, _next, and static files
    '/((?!api|_next|.*\\..*).*)',
  ],
};
