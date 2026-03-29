import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode, createElement } from 'react';

const mockApiGet = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
  },
}));

import { useAuditLogs, auditLogKeys } from './useAuditLogs';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

describe('auditLogKeys', () => {
  it('should have correct base key', () => {
    expect(auditLogKeys.all).toEqual(['audit', 'logs']);
  });

  it('should include params in list key', () => {
    const params = { page: 1, pageSize: 20, module: 'identity' };
    expect(auditLogKeys.list(params)).toEqual(['audit', 'logs', 'list', params]);
  });
});

describe('useAuditLogs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should fetch paginated audit logs', async () => {
    const mockData = {
      items: [
        {
          id: 'log-1',
          module: 'identity',
          operation: 'CreateUser',
          operationType: 'Create',
          userEmail: 'admin@test.com',
          isSuccess: true,
          timestamp: '2026-03-29T10:00:00Z',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 20 };
    const { result } = renderHook(() => useAuditLogs(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.data).toEqual(mockData);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/audit/logs', {
      page: 1,
      pageSize: 20,
      module: undefined,
      isSuccess: undefined,
      dateFrom: undefined,
      dateTo: undefined,
      operation: undefined,
      userId: undefined,
      entityType: undefined,
    });
  });

  it('should pass filter params to API call', async () => {
    mockApiGet.mockResolvedValue({ items: [], totalCount: 0 });

    const params = {
      page: 2,
      pageSize: 50,
      module: 'crm',
      isSuccess: true,
      dateFrom: '2026-01-01',
      dateTo: '2026-03-29',
      operation: 'CreateLead',
      userId: 'user-1',
      entityType: 'Lead',
    };

    const { result } = renderHook(() => useAuditLogs(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/audit/logs', {
      page: 2,
      pageSize: 50,
      module: 'crm',
      isSuccess: true,
      dateFrom: '2026-01-01',
      dateTo: '2026-03-29',
      operation: 'CreateLead',
      userId: 'user-1',
      entityType: 'Lead',
    });
  });
});
