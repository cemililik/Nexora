import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode, createElement } from 'react';

const mockApiGet = vi.fn();
const mockApiPut = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    put: (...args: unknown[]) => mockApiPut(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('@/shared/hooks/useApiError', () => ({
  useApiError: () => ({
    handleApiError: vi.fn(),
  }),
}));

import {
  useAuditSettings,
  useUpdateAuditSetting,
  useBulkUpdateAuditSettings,
  auditSettingKeys,
} from './useAuditSettings';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

describe('auditSettingKeys', () => {
  it('should have correct base key', () => {
    expect(auditSettingKeys.all).toEqual(['audit', 'settings']);
  });
});

describe('useAuditSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should fetch audit settings', async () => {
    const mockSettings = [
      {
        id: 's1',
        module: 'identity',
        operation: 'CreateUser',
        isEnabled: true,
        retentionDays: 90,
      },
      {
        id: 's2',
        module: 'crm',
        operation: 'UpdateLead',
        isEnabled: false,
        retentionDays: 30,
      },
    ];
    mockApiGet.mockResolvedValue(mockSettings);

    const { result } = renderHook(() => useAuditSettings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.data).toEqual(mockSettings);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/audit/settings');
  });
});

describe('useUpdateAuditSetting', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should update a single audit setting', async () => {
    const updatedSetting = {
      id: 's1',
      module: 'identity',
      operation: 'CreateUser',
      isEnabled: false,
      retentionDays: 60,
    };
    mockApiPut.mockResolvedValue(updatedSetting);

    const { result } = renderHook(() => useUpdateAuditSetting(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(updatedSetting);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith('/audit/settings', updatedSetting);
  });
});

describe('useBulkUpdateAuditSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should bulk update audit settings', async () => {
    const bulkSettings = [
      { module: 'identity', operation: 'CreateUser', isEnabled: true, retentionDays: 90 },
      { module: 'crm', operation: 'UpdateLead', isEnabled: false, retentionDays: 30 },
    ];
    mockApiPut.mockResolvedValue(bulkSettings);

    const { result } = renderHook(() => useBulkUpdateAuditSettings(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(bulkSettings);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith('/audit/settings/bulk', {
      settings: bulkSettings,
    });
  });
});
