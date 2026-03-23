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

import { consentKeys, useConsents, useRecordConsent } from './useConsents';

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

describe('consentKeys', () => {
  it('all_WithContactId_ReturnsCorrectKey', () => {
    expect(consentKeys.all('c-1')).toEqual(['contacts', 'consents', 'c-1']);
  });

  it('all_WithDifferentContactId_ReturnsCorrectKey', () => {
    expect(consentKeys.all('c-99')).toEqual(['contacts', 'consents', 'c-99']);
  });
});

describe('useConsents', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint', async () => {
    const mockConsents = [{ id: 'cn-1', type: 'Marketing', granted: true }];
    mockApiGet.mockResolvedValue(mockConsents);

    const { result } = renderHook(() => useConsents('contact-1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/consents`,
    );
  });

  it('should not fetch when contactId is empty', () => {
    const { result } = renderHook(() => useConsents(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });

  it('should encode contactId in URL', async () => {
    mockApiGet.mockResolvedValue([]);

    const { result } = renderHook(() => useConsents('id/special'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('id/special')}/consents`,
    );
  });
});

describe('useRecordConsent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const created = { id: 'cn-2', type: 'Marketing', granted: true };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useRecordConsent('contact-1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').RecordConsentRequest = {
      consentType: 'EmailMarketing',
      granted: true,
      source: 'Form',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/consents`,
      payload,
    );
  });
});
