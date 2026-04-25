import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

const mockApiGet = vi.fn();
const mockApiPost = vi.fn();

vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
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

const scenariosFixture = [
  {
    name: 'general',
    descriptionLockey: 'lockey_demo_scenario_general_description',
    requiredModules: ['contacts'],
    optionalModules: [],
  },
  {
    name: 'ngo',
    descriptionLockey: 'lockey_demo_scenario_ngo_description',
    requiredModules: ['contacts'],
    optionalModules: [],
  },
];

describe('CreateDemoEnvironmentDialog', () => {
  beforeEach(() => {
    mockApiPost.mockReset();
    mockApiGet.mockReset();
    // T-029: dialog now fetches scenarios via GET /identity/demo/scenarios
    // before the form (and submit button) become interactive.
    mockApiGet.mockResolvedValue(scenariosFixture);
  });

  it('renders title, description, and the server-fetched scenario dropdown', async () => {
    renderWithClient(
      <CreateDemoEnvironmentDialog tenantId="t-1" open onOpenChange={vi.fn()} />,
    );

    expect(screen.getByText('lockey_identity_tenants_demo_dialog_title')).toBeInTheDocument();
    expect(screen.getByText('lockey_identity_tenants_demo_dialog_description')).toBeInTheDocument();
    // FormField label appears once scenarios resolve.
    await waitFor(() => {
      expect(
        screen.getByText('lockey_identity_tenants_demo_scenario_label'),
      ).toBeInTheDocument();
    });
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

    // Wait for scenarios to load + auto-pick "general" via dropdown click is
    // not necessary because the form starts with empty scenario; pick it
    // explicitly via the underlying Select.
    const submitBtn = await screen.findByRole('button', {
      name: 'lockey_identity_tenants_demo_submit',
    });
    // Default scenario is empty; click "general" option to enable submit.
    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.click(
      await screen.findByRole('option', {
        name: 'lockey_identity_tenants_demo_scenario_general',
      }),
    );
    await userEvent.click(submitBtn);

    await waitFor(() => {
      // Third arg is the AxiosRequestConfig that the hook now threads
      // an AbortSignal through (round-4 review). Match it loosely so the
      // test asserts on the URL + payload + the *presence* of a signal,
      // not the controller identity.
      expect(mockApiPost).toHaveBeenCalledWith(
        '/identity/tenants/demo',
        { tenantId: 't-1', scenario: 'general' },
        expect.objectContaining({ signal: expect.any(AbortSignal) }),
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
      screen.queryByRole('button', { name: 'lockey_identity_tenants_demo_submit' }),
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

    const submitBtn = await screen.findByRole('button', {
      name: 'lockey_identity_tenants_demo_submit',
    });
    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.click(
      await screen.findByRole('option', {
        name: 'lockey_identity_tenants_demo_scenario_general',
      }),
    );
    await userEvent.click(submitBtn);

    await waitFor(() => {
      expect(screen.getByText(/timeout/)).toBeInTheDocument();
    });
  });
});
