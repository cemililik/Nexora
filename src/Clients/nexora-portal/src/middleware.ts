import { type NextRequest, NextResponse } from 'next/server';
import createIntlMiddleware from 'next-intl/middleware';

import { auth } from '@/shared/lib/auth';
import { routing } from './i18n/routing';

const intlMiddleware = createIntlMiddleware(routing);

const publicPaths = ['/auth/login', '/auth/callback'];

function isPublicPath(pathname: string): boolean {
  return publicPaths.some((p) => pathname.includes(p));
}

export default auth((req) => {
  const isAuth = !!req.auth;
  const { pathname } = req.nextUrl;

  // Allow public paths without auth
  if (isPublicPath(pathname)) {
    return intlMiddleware(req as unknown as NextRequest);
  }

  // Redirect unauthenticated users to login
  if (!isAuth) {
    const locale = pathname.split('/')[1] || 'en';
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
