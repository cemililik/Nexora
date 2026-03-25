import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';

// Mock next-auth/react
const mockUseSession = vi.fn();
const mockSignOut = vi.fn();
vi.mock('next-auth/react', () => ({
  useSession: () => mockUseSession(),
  signOut: (...args: unknown[]) => mockSignOut(...args),
}));

// Mock next-intl
vi.mock('next-intl', () => ({
  useLocale: () => 'en',
  useTranslations: () => (key: string) => key,
}));

// Mock api
const mockApiGet = vi.fn();
const mockSetAuthToken = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: { get: (...args: unknown[]) => mockApiGet(...args) },
  setAuthToken: (...args: unknown[]) => mockSetAuthToken(...args),
}));

// Mock sonner
const mockToastError = vi.fn();
vi.mock('sonner', () => ({
  toast: { error: (...args: unknown[]) => mockToastError(...args) },
}));

// Mock authStore
const mockSetSession = vi.fn();
const mockClearSession = vi.fn();
let storeUser: unknown = null;
const mockGetState = () => ({ user: storeUser });
vi.mock('@/shared/lib/stores/authStore', () => ({
  useAuthStore: Object.assign(
    () => ({
      setSession: mockSetSession,
      clearSession: mockClearSession,
      user: storeUser,
      isAuthenticated: !!storeUser,
    }),
    { getState: () => mockGetState() },
  ),
}));

// Import after mocks
import { useAuth } from './useAuth';

describe('useAuth', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    storeUser = null;
  });

  it('should return isLoading true when session is loading', () => {
    mockUseSession.mockReturnValue({ status: 'loading', data: null });

    const { result } = renderHook(() => useAuth());

    expect(result.current.isLoading).toBe(true);
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('should set auth token and fetch user when authenticated', async () => {
    const mockUser = { id: '1', firstName: 'John', lastName: 'Doe' };
    mockUseSession.mockReturnValue({
      status: 'authenticated',
      data: {
        accessToken: 'test-token',
        tenantId: 'tenant-1',
        organizationId: 'org-1',
        permissions: ['read'],
      },
    });
    mockApiGet.mockResolvedValue(mockUser);

    renderHook(() => useAuth());

    await waitFor(() => {
      expect(mockSetAuthToken).toHaveBeenCalledWith('test-token');
      expect(mockApiGet).toHaveBeenCalledWith('/identity/users/me');
      expect(mockSetSession).toHaveBeenCalledWith({
        user: mockUser,
        tenantId: 'tenant-1',
        organizationId: 'org-1',
        permissions: ['read'],
      });
    });
  });

  it('should show toast on API fetch failure when no user in store', async () => {
    mockUseSession.mockReturnValue({
      status: 'authenticated',
      data: {
        accessToken: 'test-token',
        tenantId: 'tenant-1',
        permissions: [],
      },
    });
    mockApiGet.mockRejectedValue(new Error('Network error'));

    renderHook(() => useAuth());

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('lockey_error_session_expired');
    });
  });

  it('should sign out on RefreshAccessTokenError', () => {
    mockUseSession.mockReturnValue({
      status: 'authenticated',
      data: { error: 'RefreshAccessTokenError' },
    });

    renderHook(() => useAuth());

    expect(mockSignOut).toHaveBeenCalledWith({
      callbackUrl: '/en/auth/login',
    });
  });

  it('should clear session when unauthenticated and no user in store', async () => {
    // First authenticate to set hasInitialized
    const mockUser = { id: '1', firstName: 'Test', lastName: 'User' };
    mockUseSession.mockReturnValue({
      status: 'authenticated',
      data: { accessToken: 'token', tenantId: 't', permissions: [] },
    });
    mockApiGet.mockResolvedValue(mockUser);

    const { rerender } = renderHook(() => useAuth());

    await waitFor(() => {
      expect(mockSetAuthToken).toHaveBeenCalledWith('token');
    });

    // Now transition to unauthenticated
    vi.clearAllMocks();
    mockUseSession.mockReturnValue({ status: 'unauthenticated', data: null });

    rerender();

    await waitFor(() => {
      expect(mockSetAuthToken).toHaveBeenCalledWith(null);
      expect(mockClearSession).toHaveBeenCalled();
    });
  });

  it('should not re-fetch user when already loaded', () => {
    storeUser = { id: '1', firstName: 'John' };
    mockUseSession.mockReturnValue({
      status: 'authenticated',
      data: { accessToken: 'token', tenantId: 't', permissions: [] },
    });

    renderHook(() => useAuth());

    expect(mockApiGet).not.toHaveBeenCalled();
  });
});
