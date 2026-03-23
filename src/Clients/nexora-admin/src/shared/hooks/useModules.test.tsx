import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
const mockApiGet = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: { get: (...args: unknown[]) => mockApiGet(...args) },
}));

// Mock auth store
let storeTenantId: string | null = null;
let storeToken: string | null = 'mock-token';
vi.mock('@/shared/lib/stores/authStore', () => ({
  useAuthStore: (selector: (s: { tenantId: string | null; token: string | null }) => unknown) =>
    selector({ tenantId: storeTenantId, token: storeToken }),
}));

// Mock registry
vi.mock('@/modules/_registry', () => ({
  allAdminModules: [
    { name: 'contacts', icon: 'Users', routes: [], navigation: [], permissions: [] },
    { name: 'documents', icon: 'FileText', routes: [], navigation: [], permissions: [] },
  ],
}));

import { useModules } from './useModules';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        {children}
      </QueryClientProvider>
    );
  };
}

describe('useModules', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    storeTenantId = null;
    storeToken = 'mock-token';
  });

  it('should not fetch when tenantId is null', () => {
    storeTenantId = null;
    const { result } = renderHook(() => useModules(), { wrapper: createWrapper() });
    expect(mockApiGet).not.toHaveBeenCalled();
    expect(result.current.activeModules).toEqual([]);
  });

  it('should not fetch when tenantId is not a valid UUID', () => {
    storeTenantId = 'not-a-uuid';
    renderHook(() => useModules(), { wrapper: createWrapper() });
    expect(mockApiGet).not.toHaveBeenCalled();
  });

  it('should filter registry by installed active modules', async () => {
    storeTenantId = '550e8400-e29b-41d4-a716-446655440000';
    mockApiGet.mockResolvedValue([
      { id: '1', moduleName: 'contacts', isActive: true, installedAt: '2026-01-01' },
      { id: '2', moduleName: 'crm', isActive: true, installedAt: '2026-01-01' },
    ]);

    const { result } = renderHook(() => useModules(), { wrapper: createWrapper() });

    await waitFor(() => {
      expect(result.current.activeModules).toHaveLength(1);
      expect(result.current.activeModules[0]?.name).toBe('contacts');
    });
  });

  it('should exclude inactive modules', async () => {
    storeTenantId = '550e8400-e29b-41d4-a716-446655440000';
    mockApiGet.mockResolvedValue([
      { id: '1', moduleName: 'contacts', isActive: false, installedAt: '2026-01-01' },
    ]);

    const { result } = renderHook(() => useModules(), { wrapper: createWrapper() });

    await waitFor(() => {
      expect(result.current.activeModules).toHaveLength(0);
    });
  });

  it('should not fetch when token is null', () => {
    storeTenantId = '550e8400-e29b-41d4-a716-446655440000';
    storeToken = null;
    const { result } = renderHook(() => useModules(), { wrapper: createWrapper() });
    expect(mockApiGet).not.toHaveBeenCalled();
    expect(result.current.activeModules).toEqual([]);
  });

  it('should not fetch when token is an error object', () => {
    storeTenantId = '550e8400-e29b-41d4-a716-446655440000';
    storeToken = { error: 'RefreshAccessTokenError' } as unknown as string;
    const { result } = renderHook(() => useModules(), { wrapper: createWrapper() });
    expect(mockApiGet).not.toHaveBeenCalled();
    expect(result.current.activeModules).toEqual([]);
  });

  it('should provide hasModule helper', async () => {
    storeTenantId = '550e8400-e29b-41d4-a716-446655440000';
    mockApiGet.mockResolvedValue([
      { id: '1', moduleName: 'contacts', isActive: true, installedAt: '2026-01-01' },
    ]);

    const { result } = renderHook(() => useModules(), { wrapper: createWrapper() });

    await waitFor(() => {
      expect(result.current.hasModule('contacts')).toBe(true);
      expect(result.current.hasModule('documents')).toBe(false);
    });
  });
});
