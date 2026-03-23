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
  noteKeys,
  useNotes,
  useAddNote,
  useUpdateNote,
  useDeleteNote,
  usePinNote,
} from './useNotes';

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

describe('noteKeys', () => {
  it('all_WithContactId_ReturnsBaseKey', () => {
    expect(noteKeys.all('c1')).toEqual(['contacts', 'notes', 'c1']);
  });
});

describe('useNotes', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded contactId', async () => {
    const mockNotes = [{ id: 'n1', content: 'Follow up' }];
    mockApiGet.mockResolvedValue(mockNotes);

    const { result } = renderHook(() => useNotes('c1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/notes`,
    );
  });

  it('should not fetch when contactId is empty', async () => {
    const { result } = renderHook(() => useNotes(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useAddNote', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded contactId and payload', async () => {
    const created = { id: 'n1', content: 'New note' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useAddNote('c1'), {
      wrapper: createWrapper(),
    });

    const payload = { content: 'New note' };
    result.current.mutate(payload as never);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/notes`,
      payload,
    );
  });
});

describe('useUpdateNote', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with encoded contactId, noteId, and payload', async () => {
    const updated = { id: 'n1', content: 'Updated note' };
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(() => useUpdateNote('c1'), {
      wrapper: createWrapper(),
    });

    const data = { content: 'Updated note' };
    result.current.mutate({ noteId: 'n1', data } as never);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/notes/${encodeURIComponent('n1')}`,
      data,
    );
  });
});

describe('useDeleteNote', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded contactId and noteId', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useDeleteNote('c1'), {
      wrapper: createWrapper(),
    });

    result.current.mutate('n1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/notes/${encodeURIComponent('n1')}`,
    );
  });
});

describe('usePinNote', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with encoded contactId, noteId, and pin payload', async () => {
    mockApiPut.mockResolvedValue(undefined);

    const { result } = renderHook(() => usePinNote('c1'), {
      wrapper: createWrapper(),
    });

    const data = { isPinned: true };
    result.current.mutate({ noteId: 'n1', data } as never);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/notes/${encodeURIComponent('n1')}/pin`,
      data,
    );
  });
});
