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

import type { AddAddressRequest, UpdateAddressRequest } from '../types';
import {
  addressKeys,
  useAddresses,
  useAddAddress,
  useUpdateAddress,
  useDeleteAddress,
} from './useAddresses';

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

describe('addressKeys', () => {
  it('all_WithContactId_ReturnsBaseKey', () => {
    expect(addressKeys.all('c1')).toEqual(['contacts', 'addresses', 'c1']);
  });

  it('list_WithContactId_ReturnsListKey', () => {
    expect(addressKeys.list('c1')).toEqual([
      'contacts',
      'addresses',
      'c1',
      'list',
    ]);
  });
});

describe('useAddresses', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded contactId', async () => {
    const mockAddresses = [{ id: 'a1', street: '123 Main St' }];
    mockApiGet.mockResolvedValue(mockAddresses);

    const { result } = renderHook(() => useAddresses('c1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/addresses`,
    );
  });

  it('should not fetch when contactId is empty', async () => {
    const { result } = renderHook(() => useAddresses(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useAddAddress', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded contactId and payload', async () => {
    const created = { id: 'a1', street: '456 Oak Ave' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useAddAddress('c1'), {
      wrapper: createWrapper(),
    });

    const payload: AddAddressRequest = {
      type: 'Home',
      street1: '456 Oak Ave',
      city: 'Springfield',
      countryCode: 'US',
      isPrimary: false,
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/addresses`,
      payload,
    );
  });
});

describe('useUpdateAddress', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with encoded contactId, addressId, and payload', async () => {
    const updated = { id: 'a1', street: '789 Elm Blvd' };
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(() => useUpdateAddress('c1'), {
      wrapper: createWrapper(),
    });

    const data: UpdateAddressRequest = {
      type: 'Home',
      street1: '789 Elm Blvd',
      city: 'Springfield',
      countryCode: 'US',
    };
    result.current.mutate({ addressId: 'a1', data });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/addresses/${encodeURIComponent('a1')}`,
      data,
    );
  });
});

describe('useDeleteAddress', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded contactId and addressId', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useDeleteAddress('c1'), {
      wrapper: createWrapper(),
    });

    result.current.mutate('a1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/addresses/${encodeURIComponent('a1')}`,
    );
  });
});
