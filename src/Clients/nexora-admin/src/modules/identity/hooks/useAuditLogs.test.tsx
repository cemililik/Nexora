import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
const mockApiGet = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: { get: (...args: unknown[]) => mockApiGet(...args) },
}));

import { auditKeys, useAuditLogs } from './useAuditLogs';

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

describe('auditKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(auditKeys.all).toEqual(['identity', 'audit-logs']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 1, pageSize: 20 };
    expect(auditKeys.list(params)).toEqual([
      'identity',
      'audit-logs',
      'list',
      params,
    ]);
  });

  it('list_WithFilters_IncludesAllFilters', () => {
    const params = {
      userId: 'user-1',
      action: 'Login',
      from: '2026-01-01',
      to: '2026-01-31',
      page: 2,
      pageSize: 50,
    };
    expect(auditKeys.list(params)).toEqual([
      'identity',
      'audit-logs',
      'list',
      params,
    ]);
  });
});

describe('useAuditLogs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and default pagination', async () => {
    const mockData = {
      items: [{ id: 'log-1', userId: 'user-1', action: 'Login', timestamp: '2026-01-15T10:00:00Z' }],
      totalCount: 1,
    };
    mockApiGet.mockResolvedValue(mockData);

    const { result } = renderHook(() => useAuditLogs({}), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/audit-logs', {
      page: 1,
      pageSize: 20,
    });
  });

  it('should pass filter params when provided', async () => {
    mockApiGet.mockResolvedValue({ items: [], totalCount: 0 });

    const params = {
      userId: 'user-42',
      action: 'CreateUser',
      from: '2026-03-01',
      to: '2026-03-22',
      page: 2,
      pageSize: 50,
    };

    const { result } = renderHook(() => useAuditLogs(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/audit-logs', {
      userId: 'user-42',
      action: 'CreateUser',
      from: '2026-03-01',
      to: '2026-03-22',
      page: 2,
      pageSize: 50,
    });
  });

  it('should omit undefined filter fields', async () => {
    mockApiGet.mockResolvedValue({ items: [], totalCount: 0 });

    const { result } = renderHook(
      () => useAuditLogs({ userId: 'user-1', page: 3 }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/audit-logs', {
      userId: 'user-1',
      page: 3,
      pageSize: 20,
    });
  });
});
