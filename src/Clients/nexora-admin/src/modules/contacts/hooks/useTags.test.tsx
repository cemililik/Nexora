import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
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
  tagKeys,
  useTags,
  useCreateTag,
  useUpdateTag,
  useDeleteTag,
  useAssignTag,
  useRemoveTag,
} from './useTags';

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

describe('tagKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(tagKeys.all).toEqual(['contacts', 'tags']);
  });

  it('list_WithoutParams_ReturnsListKey', () => {
    expect(tagKeys.list()).toEqual(['contacts', 'tags', 'list', undefined]);
  });

  it('list_WithCategory_IncludesParams', () => {
    const params: { category: import('../types').TagCategory } = { category: 'Donor' };
    expect(tagKeys.list(params)).toEqual([
      'contacts',
      'tags',
      'list',
      params,
    ]);
  });
});

describe('useTags', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and no category', async () => {
    const mockTags = [{ id: 't1', name: 'VIP' }];
    mockApiGet.mockResolvedValue(mockTags);

    const { result } = renderHook(() => useTags(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/contacts/tags', {
      category: undefined,
    });
  });

  it('should pass category filter when provided', async () => {
    mockApiGet.mockResolvedValue([]);

    const { result } = renderHook(() => useTags({ category: 'Volunteer' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/contacts/tags', {
      category: 'Volunteer',
    });
  });
});

describe('useCreateTag', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const created = { id: 't1', name: 'VIP' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useCreateTag(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').CreateTagRequest = {
      name: 'VIP',
      category: 'Donor',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/contacts/tags', payload);
  });
});

describe('useUpdateTag', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with encoded id and payload', async () => {
    const updated = { id: 't1', name: 'Premium' };
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(() => useUpdateTag('t1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').UpdateTagRequest = {
      name: 'Premium',
      category: 'Donor',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/contacts/tags/${encodeURIComponent('t1')}`,
      payload,
    );
  });
});

describe('useDeleteTag', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded id', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useDeleteTag(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('t1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/contacts/tags/${encodeURIComponent('t1')}`,
    );
  });
});

describe('useAssignTag', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded contactId and tagId', async () => {
    mockApiPost.mockResolvedValue(undefined);

    const { result } = renderHook(() => useAssignTag(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ contactId: 'c1', tagId: 't1' });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/tags/${encodeURIComponent('t1')}`,
    );
  });
});

describe('useRemoveTag', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded contactId and tagId', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useRemoveTag(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ contactId: 'c1', tagId: 't1' });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/tags/${encodeURIComponent('t1')}`,
    );
  });
});
