import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
const mockApiGet = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: { get: (...args: unknown[]) => mockApiGet(...args) },
}));

import { orgKeys, useOrganizations } from './useOrganizations';

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

describe('orgKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(orgKeys.all).toEqual(['identity', 'organizations']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 1, pageSize: 20 };
    expect(orgKeys.list(params)).toEqual([
      'identity',
      'organizations',
      'list',
      params,
    ]);
  });

  it('detail_WithId_IncludesId', () => {
    expect(orgKeys.detail('org-1')).toEqual([
      'identity',
      'organizations',
      'detail',
      'org-1',
    ]);
  });

  it('members_WithIdAndParams_IncludesBoth', () => {
    const params = { page: 1, pageSize: 10 };
    expect(orgKeys.members('org-1', params)).toEqual([
      'identity',
      'organizations',
      'members',
      'org-1',
      params,
    ]);
  });
});

describe('useOrganizations', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and params', async () => {
    const mockData = {
      items: [{ id: 'org-1', name: 'Acme Corp', slug: 'acme' }],
      totalCount: 1,
    };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 15 };
    const { result } = renderHook(() => useOrganizations(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/organizations', {
      page: 1,
      pageSize: 15,
    });
  });

  it('should return paginated data', async () => {
    const mockData = {
      items: [
        { id: 'org-1', name: 'Acme Corp' },
        { id: 'org-2', name: 'Globex' },
      ],
      totalCount: 2,
    };
    mockApiGet.mockResolvedValue(mockData);

    const { result } = renderHook(
      () => useOrganizations({ page: 1, pageSize: 10 }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => {
      expect(result.current.data).toEqual(mockData);
    });
  });
});
