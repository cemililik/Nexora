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

import type { AddRelationshipRequest } from '../types';
import {
  relationshipKeys,
  useRelationships,
  useAddRelationship,
  useRemoveRelationship,
} from './useRelationships';

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

describe('relationshipKeys', () => {
  it('all_WithContactId_ReturnsBaseKey', () => {
    expect(relationshipKeys.all('c1')).toEqual([
      'contacts',
      'relationships',
      'c1',
    ]);
  });
});

describe('useRelationships', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded contactId', async () => {
    const mockData = [{ id: 'r1', relatedContactId: 'c2', type: 'SpouseOf' }];
    mockApiGet.mockResolvedValue(mockData);

    const { result } = renderHook(() => useRelationships('c1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/relationships`,
    );
  });

  it('should not fetch when contactId is empty', async () => {
    const { result } = renderHook(() => useRelationships(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useAddRelationship', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded contactId and payload', async () => {
    const created = { id: 'r1', relatedContactId: 'c2', type: 'SpouseOf' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useAddRelationship('c1'), {
      wrapper: createWrapper(),
    });

    const payload: AddRelationshipRequest = {
      relatedContactId: 'c2',
      type: 'SpouseOf',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/relationships`,
      payload,
    );
  });
});

describe('useRemoveRelationship', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded contactId and relationshipId', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useRemoveRelationship('c1'), {
      wrapper: createWrapper(),
    });

    result.current.mutate('r1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/relationships/${encodeURIComponent('r1')}`,
    );
  });
});
