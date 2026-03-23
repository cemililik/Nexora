import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
const mockApiGet = vi.fn();
const mockApiPost = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    post: (...args: unknown[]) => mockApiPost(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}));

import { duplicateKeys, useDuplicates, useMergeContacts } from './useDuplicates';

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

describe('duplicateKeys', () => {
  it('all_WithContactId_ReturnsCorrectKey', () => {
    expect(duplicateKeys.all('c-1')).toEqual([
      'contacts',
      'duplicates',
      'c-1',
    ]);
  });

  it('all_WithDifferentContactId_ReturnsCorrectKey', () => {
    expect(duplicateKeys.all('c-77')).toEqual([
      'contacts',
      'duplicates',
      'c-77',
    ]);
  });
});

describe('useDuplicates', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and no threshold', async () => {
    const mockDuplicates = [{ id: 'd-1', matchScore: 0.95, contactId: 'c-2' }];
    mockApiGet.mockResolvedValue(mockDuplicates);

    const { result } = renderHook(() => useDuplicates('contact-1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/duplicates`,
      {},
    );
  });

  it('should pass threshold param when provided', async () => {
    mockApiGet.mockResolvedValue([]);

    const { result } = renderHook(() => useDuplicates('contact-1', 0.8), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/duplicates`,
      { threshold: 0.8 },
    );
  });

  it('should not fetch when contactId is empty', () => {
    const { result } = renderHook(() => useDuplicates(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });

  it('should encode contactId in URL', async () => {
    mockApiGet.mockResolvedValue([]);

    const { result } = renderHook(() => useDuplicates('id/special'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('id/special')}/duplicates`,
      {},
    );
  });
});

describe('useMergeContacts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const mergeResult = { mergedContactId: 'c-1', removedContactIds: ['c-2'] };
    mockApiPost.mockResolvedValue(mergeResult);

    const { result } = renderHook(() => useMergeContacts(), {
      wrapper: createWrapper(),
    });

    const payload = { primaryContactId: 'c-1', secondaryContactIds: ['c-2'] };
    result.current.mutate(payload as never);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      '/contacts/contacts/merge',
      payload,
    );
  });
});
