import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
const mockApiGet = vi.fn();
const mockApiPost = vi.fn();
const mockApiDelete = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    post: (...args: unknown[]) => mockApiPost(...args),
    delete: (...args: unknown[]) => mockApiDelete(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

import { moduleKeys, useTenantModules, useInstallModule, useUninstallModule } from './useModuleManagement';

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

describe('moduleKeys', () => {
  it('all_WithTenantId_ReturnsCorrectKey', () => {
    expect(moduleKeys.all('t1')).toEqual(['identity', 'modules', 't1']);
  });
});

describe('useTenantModules', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct tenant modules endpoint', async () => {
    const mockModules = [
      { moduleName: 'contacts', installedAt: '2025-01-01' },
    ];
    mockApiGet.mockResolvedValue(mockModules);

    const { result } = renderHook(() => useTenantModules('tenant-1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/tenants/tenant-1/modules');
  });

  it('should not fetch when tenantId is empty', () => {
    const { result } = renderHook(() => useTenantModules(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useInstallModule', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const installedModule = { moduleName: 'crm', installedAt: '2025-01-01' };
    mockApiPost.mockResolvedValue(installedModule);

    const { result } = renderHook(() => useInstallModule('tenant-1'), {
      wrapper: createWrapper(),
    });

    result.current.mutate('crm');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      '/identity/tenants/tenant-1/modules',
      { moduleName: 'crm' },
    );
  });
});

describe('useUninstallModule', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with correct endpoint', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useUninstallModule('tenant-1'), {
      wrapper: createWrapper(),
    });

    result.current.mutate('crm');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      '/identity/tenants/tenant-1/modules/crm',
    );
  });

  it('should encode special characters in tenantId and moduleName', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useUninstallModule('tenant/special'), {
      wrapper: createWrapper(),
    });

    result.current.mutate('mod/name');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      '/identity/tenants/tenant%2Fspecial/modules/mod%2Fname',
    );
  });
});
