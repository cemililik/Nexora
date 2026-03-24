import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { NextRequest } from 'next/server';
import { NextResponse } from 'next/server';

// Mock next-auth/jwt
const mockGetToken = vi.fn();
vi.mock('next-auth/jwt', () => ({
  getToken: (...args: unknown[]) => mockGetToken(...args),
}));

// Mock next-intl/middleware — returns a passthrough middleware
const mockIntlMiddleware = vi.fn(
  () => NextResponse.next(),
);
vi.mock('next-intl/middleware', () => ({
  default: () => mockIntlMiddleware,
}));

// Mock routing config
vi.mock('./i18n/routing', () => ({
  routing: {
    locales: ['en', 'tr'],
    defaultLocale: 'en',
  },
}));

function createMockRequest(pathname: string): NextRequest {
  const url = new URL(pathname, 'http://localhost:3000');
  return {
    nextUrl: url,
    url: url.toString(),
  } as unknown as NextRequest;
}

// Import after mocks are set up
const { middleware } = await import('./middleware');

describe('middleware', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    process.env.AUTH_SECRET = 'test-secret';
  });

  it('should_AllowPublicPaths_WithoutAuthCheck', async () => {
    const request = createMockRequest('/en/auth/login');
    await middleware(request);

    expect(mockGetToken).not.toHaveBeenCalled();
    expect(mockIntlMiddleware).toHaveBeenCalledWith(request);
  });

  it('should_AllowPublicCallbackPath_WithoutAuthCheck', async () => {
    const request = createMockRequest('/tr/auth/callback');
    await middleware(request);

    expect(mockGetToken).not.toHaveBeenCalled();
    expect(mockIntlMiddleware).toHaveBeenCalledWith(request);
  });

  it('should_AllowPublicPathsWithoutLocalePrefix_WithoutAuthCheck', async () => {
    const request = createMockRequest('/auth/login');
    await middleware(request);

    expect(mockGetToken).not.toHaveBeenCalled();
    expect(mockIntlMiddleware).toHaveBeenCalledWith(request);
  });

  it('should_RedirectToLogin_WhenNoToken', async () => {
    mockGetToken.mockResolvedValue(null);
    const request = createMockRequest('/en/dashboard');

    const response = await middleware(request);

    expect(mockGetToken).toHaveBeenCalled();
    expect(response.status).toBe(307);
    expect(new URL(response.headers.get('location')!).pathname).toBe('/en/auth/login');
  });

  it('should_RedirectToLogin_WhenRefreshAccessTokenError', async () => {
    mockGetToken.mockResolvedValue({ error: 'RefreshAccessTokenError' });
    const request = createMockRequest('/tr/dashboard');

    const response = await middleware(request);

    expect(response.status).toBe(307);
    expect(new URL(response.headers.get('location')!).pathname).toBe('/tr/auth/login');
  });

  it('should_UseDefaultLocale_WhenNoLocaleInPath', async () => {
    mockGetToken.mockResolvedValue(null);
    const request = createMockRequest('/dashboard');

    const response = await middleware(request);

    expect(response.status).toBe(307);
    expect(new URL(response.headers.get('location')!).pathname).toBe('/en/auth/login');
  });

  it('should_AllowAuthenticatedRequests_WithIntlHandling', async () => {
    mockGetToken.mockResolvedValue({ sub: 'user-1' });
    const request = createMockRequest('/en/dashboard');

    await middleware(request);

    expect(mockGetToken).toHaveBeenCalled();
    expect(mockIntlMiddleware).toHaveBeenCalledWith(request);
  });
});
