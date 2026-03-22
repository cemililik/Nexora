import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
const mockApiGet = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: { get: (...args: unknown[]) => mockApiGet(...args) },
}));

import { tenantKeys, useTenants } from './useTenants';

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

describe('tenantKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(tenantKeys.all).toEqual(['identity', 'tenants']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 3, pageSize: 50 };
    expect(tenantKeys.list(params)).toEqual([
      'identity',
      'tenants',
      'list',
      params,
    ]);
  });

  it('detail_WithId_IncludesId', () => {
    expect(tenantKeys.detail('tenant-abc')).toEqual([
      'identity',
      'tenants',
      'detail',
      'tenant-abc',
    ]);
  });
});

describe('useTenants', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and params', async () => {
    const mockData = {
      items: [{ id: 't-1', name: 'Demo Tenant', slug: 'demo', status: 'Active' }],
      totalCount: 1,
    };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 10 };
    const { result } = renderHook(() => useTenants(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/tenants', {
      page: 1,
      pageSize: 10,
    });
  });

  it('should return paginated data', async () => {
    const mockData = {
      items: [{ id: 't-1', name: 'Tenant A' }],
      totalCount: 1,
    };
    mockApiGet.mockResolvedValue(mockData);

    const { result } = renderHook(
      () => useTenants({ page: 1, pageSize: 10 }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => {
      expect(result.current.data).toEqual(mockData);
    });
  });
});
