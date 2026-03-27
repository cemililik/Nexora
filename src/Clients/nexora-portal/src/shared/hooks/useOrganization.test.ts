import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode, createElement } from 'react';

// Mock API
const mockApiGet = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: { get: (...args: unknown[]) => mockApiGet(...args) },
}));

// Mock authStore
let mockOrganizationId: string | null = null;
vi.mock('@/shared/lib/stores/authStore', () => ({
  useAuthStore: (selector: (s: Record<string, unknown>) => unknown) =>
    selector({ organizationId: mockOrganizationId }),
}));

import { useOrganization } from './useOrganization';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

describe('useOrganization', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockOrganizationId = null;
  });

  it('should return null organization when no organizationId is set', () => {
    mockOrganizationId = null;

    const { result } = renderHook(() => useOrganization(), {
      wrapper: createWrapper(),
    });

    expect(result.current.organization).toBeNull();
    expect(mockApiGet).not.toHaveBeenCalled();
  });

  it('should fetch organization when organizationId is set', async () => {
    mockOrganizationId = 'org-123';
    const mockData = {
      id: 'org-123',
      name: 'Acme Corp',
      slug: 'acme',
      logoUrl: null,
      timezone: 'UTC',
      defaultCurrency: 'USD',
      defaultLanguage: 'en',
      isActive: true,
      memberCount: 10,
    };
    mockApiGet.mockResolvedValue(mockData);

    const { result } = renderHook(() => useOrganization(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.organization).toEqual(mockData);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/organizations/org-123');
  });

  it('should return isLoading true while fetching', () => {
    mockOrganizationId = 'org-123';
    mockApiGet.mockReturnValue(new Promise(() => {})); // never resolves

    const { result } = renderHook(() => useOrganization(), {
      wrapper: createWrapper(),
    });

    expect(result.current.isLoading).toBe(true);
  });

  it('should return isError true when fetch fails', async () => {
    mockOrganizationId = 'org-123';
    mockApiGet.mockRejectedValue(new Error('Network error'));

    const { result } = renderHook(() => useOrganization(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.error).toBeDefined();
  });

  it('should encode organizationId in URL', async () => {
    mockOrganizationId = 'org with spaces';
    mockApiGet.mockResolvedValue({ id: 'org with spaces', name: 'Test' });

    renderHook(() => useOrganization(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(mockApiGet).toHaveBeenCalledWith(
        '/identity/organizations/org%20with%20spaces',
      );
    });
  });
});
