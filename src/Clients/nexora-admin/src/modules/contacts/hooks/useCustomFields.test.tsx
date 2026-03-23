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
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}));

import {
  customFieldKeys,
  useCustomFieldDefinitions,
  useCreateCustomField,
  useUpdateCustomField,
  useDeleteCustomField,
  useContactCustomFields,
  useSetCustomFieldValue,
} from './useCustomFields';

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

describe('customFieldKeys', () => {
  it('definitions_Called_ReturnsCorrectKey', () => {
    expect(customFieldKeys.definitions).toEqual(['contacts', 'custom-fields']);
  });

  it('contactFields_WithContactId_ReturnsCorrectKey', () => {
    expect(customFieldKeys.contactFields('c-1')).toEqual([
      'contacts',
      'custom-fields',
      'c-1',
    ]);
  });

  it('contactFields_WithDifferentContactId_ReturnsCorrectKey', () => {
    expect(customFieldKeys.contactFields('c-99')).toEqual([
      'contacts',
      'custom-fields',
      'c-99',
    ]);
  });
});

describe('useCustomFieldDefinitions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint', async () => {
    const mockDefs = [{ id: 'cf-1', name: 'Company Size', type: 'Number' }];
    mockApiGet.mockResolvedValue(mockDefs);

    const { result } = renderHook(() => useCustomFieldDefinitions(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/contacts/custom-fields');
  });
});

describe('useCreateCustomField', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const created = { id: 'cf-2', name: 'Industry', type: 'Text' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useCreateCustomField(), {
      wrapper: createWrapper(),
    });

    const payload = { name: 'Industry', type: 'Text' };
    result.current.mutate(payload as never);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/contacts/custom-fields', payload);
  });
});

describe('useUpdateCustomField', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with correct endpoint and payload', async () => {
    const updated = { id: 'cf-1', name: 'Company Size Updated', type: 'Number' };
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(() => useUpdateCustomField('cf-1'), {
      wrapper: createWrapper(),
    });

    const payload = { name: 'Company Size Updated' };
    result.current.mutate(payload as never);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/contacts/custom-fields/${encodeURIComponent('cf-1')}`,
      payload,
    );
  });
});

describe('useDeleteCustomField', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with correct endpoint', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useDeleteCustomField(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('cf-1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/contacts/custom-fields/${encodeURIComponent('cf-1')}`,
    );
  });
});

describe('useContactCustomFields', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint', async () => {
    const mockFields = [{ definitionId: 'cf-1', value: '50' }];
    mockApiGet.mockResolvedValue(mockFields);

    const { result } = renderHook(() => useContactCustomFields('contact-1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/custom-fields`,
    );
  });

  it('should not fetch when contactId is empty', () => {
    const { result } = renderHook(() => useContactCustomFields(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useSetCustomFieldValue', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with correct endpoint and payload', async () => {
    const updated = { definitionId: 'cf-1', value: '100' };
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(() => useSetCustomFieldValue('contact-1'), {
      wrapper: createWrapper(),
    });

    const payload = { definitionId: 'cf-1', data: { value: '100' } };
    result.current.mutate(payload as never);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/custom-fields/${encodeURIComponent('cf-1')}`,
      { value: '100' },
    );
  });
});
