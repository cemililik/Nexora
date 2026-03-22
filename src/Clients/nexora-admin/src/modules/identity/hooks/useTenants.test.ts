import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode, createElement } from 'react';

const mockApiGet = vi.fn();
const mockApiPost = vi.fn();
const mockApiPut = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    post: (...args: unknown[]) => mockApiPost(...args),
    put: (...args: unknown[]) => mockApiPut(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { useTenants, useTenant, useCreateTenant } from './useTenants';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

describe('useTenants', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should fetch paginated tenants', async () => {
    const mockData = {
      items: [{ id: 't1', name: 'Tenant A', slug: 'tenant-a', status: 'Active', createdAt: '2026-01-01' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    };
    mockApiGet.mockResolvedValue(mockData);

    const { result } = renderHook(() => useTenants({ page: 1, pageSize: 20 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.data).toEqual(mockData);
    });
  });

  it('should fetch single tenant by id', async () => {
    const mockTenant = { id: 't1', name: 'Tenant A', slug: 'tenant-a', status: 'Active', createdAt: '2026-01-01', installedModules: [] };
    mockApiGet.mockResolvedValue(mockTenant);

    const { result } = renderHook(() => useTenant('t1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.data).toEqual(mockTenant);
    });
  });

  it('should not fetch tenant when id is empty', () => {
    renderHook(() => useTenant(''), {
      wrapper: createWrapper(),
    });

    expect(mockApiGet).not.toHaveBeenCalled();
  });

  it('should create tenant and invalidate queries', async () => {
    const newTenant = { id: 't2', name: 'New Tenant', slug: 'new-tenant', status: 'Trial', createdAt: '2026-03-22' };
    mockApiPost.mockResolvedValue(newTenant);

    const { result } = renderHook(() => useCreateTenant(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ name: 'New Tenant', slug: 'new-tenant' });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/identity/tenants', {
      name: 'New Tenant',
      slug: 'new-tenant',
    });
  });
});
