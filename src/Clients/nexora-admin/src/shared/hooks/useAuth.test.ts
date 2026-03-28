import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { AxiosError, AxiosHeaders } from 'axios';

// Mock keycloak-js
const mockInit = vi.fn();
const mockLogin = vi.fn();
const mockUpdateToken = vi.fn();
let mockOnTokenExpired: (() => void) | null = null;
let mockToken: string | undefined = undefined;

vi.mock('@/shared/lib/auth', () => ({
  createKeycloak: () => ({
    init: (...args: unknown[]) => mockInit(...args),
    login: (...args: unknown[]) => mockLogin(...args),
    updateToken: (...args: unknown[]) => mockUpdateToken(...args),
    get token() { return mockToken; },
    set onTokenExpired(fn: (() => void) | null) { mockOnTokenExpired = fn; },
    get onTokenExpired() { return mockOnTokenExpired; },
  }),
  parseTokenClaims: () => ({
    sub: 'user-1',
    tenant_id: 'tenant-1',
    organization_id: 'org-1',
    permissions: ['identity.users.read'],
    preferred_username: 'admin',
    email: 'admin@nexora.io',
    exp: 9999999999,
  }),
}));

// Mock API
const mockApiGet = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: { get: (...args: unknown[]) => mockApiGet(...args) },
  setAuthToken: vi.fn(),
}));

// Mock sonner
const mockToastError = vi.fn();
vi.mock('sonner', () => ({
  toast: { error: (...args: unknown[]) => mockToastError(...args) },
}));

// Mock auth store
const mockSetSession = vi.fn();
const mockClearSession = vi.fn();
const mockUpdateTokenStore = vi.fn();
vi.mock('@/shared/lib/stores/authStore', () => ({
  useAuthStore: () => ({
    setSession: mockSetSession,
    clearSession: mockClearSession,
    updateToken: mockUpdateTokenStore,
    user: null,
    isAuthenticated: false,
    token: null,
  }),
}));

import { setAuthToken } from '@/shared/lib/api';
import { useAuth } from './useAuth';

function createAxiosError(status: number, statusText: string): AxiosError {
  return new AxiosError(statusText, String(status), undefined, undefined, {
    status,
    data: null,
    statusText,
    headers: {},
    config: { headers: new AxiosHeaders() },
  });
}

describe('useAuth', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockOnTokenExpired = null;
    mockToken = undefined;
  });

  it('should return isLoading true during initialization', () => {
    mockInit.mockReturnValue(new Promise(() => {})); // never resolves
    const { result } = renderHook(() => useAuth());
    expect(result.current.isLoading).toBe(true);
  });

  it('should set session on successful authentication', async () => {
    const mockUser = { id: 'u1', firstName: 'Admin', lastName: 'User' };
    mockToken = 'test-jwt-token';
    mockInit.mockResolvedValue(true);
    mockApiGet.mockResolvedValue(mockUser);

    renderHook(() => useAuth());

    await waitFor(() => {
      expect(mockSetSession).toHaveBeenCalledWith({
        user: mockUser,
        token: 'test-jwt-token',
        tenantId: 'tenant-1',
        organizationId: 'org-1',
        permissions: ['identity.users.read'],
      });
    });
  });

  it('should fall back to token claims when /me returns 404', async () => {
    mockToken = 'test-jwt-token';
    mockInit.mockResolvedValue(true);
    mockApiGet.mockRejectedValue(createAxiosError(404, 'Not Found'));

    renderHook(() => useAuth());

    await waitFor(() => {
      expect(mockSetSession).toHaveBeenCalledWith({
        user: {
          id: 'user-1',
          email: 'admin@nexora.io',
          firstName: 'admin',
          lastName: '',
          status: 'Active',
          organizations: [],
        },
        token: 'test-jwt-token',
        tenantId: 'tenant-1',
        organizationId: 'org-1',
        permissions: ['identity.users.read'],
      });
    });
  });

  it('should clear session and redirect when /me returns 401', async () => {
    mockToken = 'test-jwt-token';
    mockInit.mockResolvedValue(true);
    mockApiGet.mockRejectedValue(createAxiosError(401, 'Unauthorized'));

    renderHook(() => useAuth());

    await waitFor(() => {
      expect(mockClearSession).toHaveBeenCalled();
      expect(mockLogin).toHaveBeenCalled();
    });
  });

  it('should fall back to token claims when /me fails with network error', async () => {
    mockToken = 'test-jwt-token';
    mockInit.mockResolvedValue(true);
    mockApiGet.mockRejectedValue(new Error('Network Error'));

    renderHook(() => useAuth());

    await waitFor(() => {
      expect(mockSetSession).toHaveBeenCalledWith(
        expect.objectContaining({ user: expect.objectContaining({ id: 'user-1' }) }),
      );
      expect(mockLogin).not.toHaveBeenCalled();
    });
  });

  it('should fall back to token claims when /me returns 500', async () => {
    mockToken = 'test-jwt-token';
    mockInit.mockResolvedValue(true);
    mockApiGet.mockRejectedValue(createAxiosError(500, 'Internal Server Error'));

    renderHook(() => useAuth());

    await waitFor(() => {
      expect(mockSetSession).toHaveBeenCalledWith(
        expect.objectContaining({ user: expect.objectContaining({ id: 'user-1' }) }),
      );
      expect(mockLogin).not.toHaveBeenCalled();
    });
  });

  it('should clear session when not authenticated', async () => {
    mockInit.mockResolvedValue(false);

    renderHook(() => useAuth());

    await waitFor(() => {
      expect(mockClearSession).toHaveBeenCalled();
    });
  });

  it('should clear session on init failure', async () => {
    mockInit.mockRejectedValue(new Error('Keycloak down'));

    renderHook(() => useAuth());

    await waitFor(() => {
      expect(mockClearSession).toHaveBeenCalled();
    });
  });

  it('should clear auth before session when update token fails', async () => {
    mockToken = 'test-jwt-token';
    mockInit.mockResolvedValue(true);
    mockApiGet.mockResolvedValue({ id: 'u1', firstName: 'Admin', lastName: 'User' });

    renderHook(() => useAuth());

    // Wait for init to complete and onTokenExpired handler to be registered
    await waitFor(() => {
      expect(mockOnTokenExpired).not.toBeNull();
    });

    // Simulate updateToken rejection
    mockUpdateToken.mockRejectedValue(new Error('Token refresh failed'));

    const callOrder: string[] = [];
    const mockSetAuthToken = vi.mocked(setAuthToken);
    mockSetAuthToken.mockImplementation(() => { callOrder.push('setAuthToken(null)'); });
    mockClearSession.mockImplementation(() => { callOrder.push('clearSession'); });

    // Trigger the onTokenExpired handler
    mockOnTokenExpired!();

    await waitFor(() => {
      expect(mockSetAuthToken).toHaveBeenCalledWith(null);
      expect(mockClearSession).toHaveBeenCalled();
      expect(callOrder.indexOf('setAuthToken(null)')).toBeLessThan(
        callOrder.indexOf('clearSession'),
      );
    });
  });
});
