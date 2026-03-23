import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

const mockApiGet = vi.fn();
const mockApiPost = vi.fn();
const mockApiPut = vi.fn();
const mockApiDelete = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    post: (...args: unknown[]) => mockApiPost(...args),
    put: (...args: unknown[]) => mockApiPut(...args),
    delete: (...args: unknown[]) => mockApiDelete(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import {
  signatureKeys,
  useSignatures,
  useSignature,
  useCreateSignatureRequest,
  useSendSignatureRequest,
  useCancelSignatureRequest,
} from './useSignatures';

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

describe('signatureKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(signatureKeys.all).toEqual(['documents', 'signatures']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 1, pageSize: 10, documentId: 'd1' };
    expect(signatureKeys.list(params)).toEqual([
      'documents',
      'signatures',
      'list',
      params,
    ]);
  });

  it('detail_WithId_IncludesId', () => {
    expect(signatureKeys.detail('s1')).toEqual([
      'documents',
      'signatures',
      'detail',
      's1',
    ]);
  });
});

describe('useSignatures', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and params', async () => {
    const mockData = { items: [], totalCount: 0 };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 20, documentId: 'd1', status: undefined };
    const { result } = renderHook(() => useSignatures(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/documents/signatures', {
      page: 1,
      pageSize: 20,
      documentId: 'd1',
      status: undefined,
    });
  });
});

describe('useSignature', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded id', async () => {
    const mockSig = { id: 's1', status: 'Pending' };
    mockApiGet.mockResolvedValue(mockSig);

    const { result } = renderHook(() => useSignature('s1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/documents/signatures/${encodeURIComponent('s1')}`,
    );
  });

  it('should not fetch when id is empty', () => {
    const { result } = renderHook(() => useSignature(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useCreateSignatureRequest', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const created = { id: 's1', documentId: 'd1' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useCreateSignatureRequest(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').CreateSignatureRequestRequest = { documentId: 'd1', title: 'Sign Contract', recipients: [{ contactId: 'c1', email: 'signer@test.com', name: 'Test Signer', signingOrder: 1 }] };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/documents/signatures', payload);
  });
});

describe('useSendSignatureRequest', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded id and /send suffix', async () => {
    mockApiPost.mockResolvedValue(undefined);

    const { result } = renderHook(() => useSendSignatureRequest(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('s1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/documents/signatures/${encodeURIComponent('s1')}/send`,
    );
  });
});

describe('useCancelSignatureRequest', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded id', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useCancelSignatureRequest(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('s1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/documents/signatures/${encodeURIComponent('s1')}`,
    );
  });
});
