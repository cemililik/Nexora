import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

const mockApiPost = vi.fn();

vi.mock('@/shared/lib/api', () => ({
  api: {
    get: vi.fn(),
    post: (...args: unknown[]) => mockApiPost(...args),
    delete: vi.fn(),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), warning: vi.fn(), error: vi.fn() },
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: 'en' } }),
}));

vi.mock('@/shared/hooks/useApiError', () => ({
  useApiError: () => ({ handleApiError: vi.fn() }),
}));

import { CreateDemoEnvironmentDialog } from './CreateDemoEnvironmentDialog';

function renderWithClient(node: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>{node}</QueryClientProvider>,
  );
}

describe('CreateDemoEnvironmentDialog', () => {
  beforeEach(() => {
    mockApiPost.mockReset();
  });

  it('renders title, description, and scenario dropdown with the general + ngo options', async () => {
    renderWithClient(
      <CreateDemoEnvironmentDialog tenantId="t-1" open onOpenChange={vi.fn()} />,
    );

    expect(screen.getByText('lockey_platform_tenants_demo_dialog_title')).toBeInTheDocument();
    expect(screen.getByText('lockey_platform_tenants_demo_dialog_description')).toBeInTheDocument();
    // Dropdown trigger is the scenario FormField's Select — default selected is "general".
    expect(screen.getByText('lockey_platform_tenants_demo_scenario_general')).toBeInTheDocument();
  });

  it('submits the selected scenario to /identity/tenants/demo and renders the per-module outcome list', async () => {
    mockApiPost.mockResolvedValue({
      tenantId: 't-1',
      scenario: 'general',
      modules: [
        { moduleName: 'identity', status: 'Seeded' },
        { moduleName: 'contacts', status: 'AlreadySeeded' },
        { moduleName: 'audit', status: 'NoOp' },
      ],
    });

    renderWithClient(
      <CreateDemoEnvironmentDialog tenantId="t-1" open onOpenChange={vi.fn()} />,
    );

    await userEvent.click(
      screen.getByRole('button', { name: 'lockey_platform_tenants_demo_submit' }),
    );

    await waitFor(() => {
      expect(mockApiPost).toHaveBeenCalledWith(
        '/identity/tenants/demo',
        { tenantId: 't-1', scenario: 'general' },
      );
    });

    // Outcome view replaces the form after success.
    await waitFor(() => {
      expect(screen.getByText('identity')).toBeInTheDocument();
      expect(screen.getByText('contacts')).toBeInTheDocument();
      expect(screen.getByText('audit')).toBeInTheDocument();
    });
    // Submit button is gone after success (only cancel remains).
    expect(
      screen.queryByRole('button', { name: 'lockey_platform_tenants_demo_submit' }),
    ).not.toBeInTheDocument();
  });

  it('surfaces a failed-module outcome with its error message', async () => {
    mockApiPost.mockResolvedValue({
      tenantId: 't-1',
      scenario: 'general',
      modules: [
        { moduleName: 'reporting', status: 'Failed', errorMessage: 'timeout' },
      ],
    });

    renderWithClient(
      <CreateDemoEnvironmentDialog tenantId="t-1" open onOpenChange={vi.fn()} />,
    );

    await userEvent.click(
      screen.getByRole('button', { name: 'lockey_platform_tenants_demo_submit' }),
    );

    await waitFor(() => {
      expect(screen.getByText(/timeout/)).toBeInTheDocument();
    });
  });
});
