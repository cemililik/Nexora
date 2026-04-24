import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';

/**
 * Per-module seed outcome returned by POST /identity/tenants/demo — matches
 * the backend `DemoSeedModuleOutcome` shape (T-005 / T-008).
 */
export interface DemoSeedModuleOutcome {
  moduleName: string;
  status: 'Seeded' | 'AlreadySeeded' | 'NoOp' | 'Failed';
  errorMessage?: string | null;
}

export interface DemoSeedRunResult {
  tenantId: string;
  scenario: string;
  modules: DemoSeedModuleOutcome[];
}

interface CreateDemoEnvironmentPayload {
  tenantId: string;
  scenario: string;
}

/**
 * Platform-only mutation that seeds demo data into an existing tenant schema.
 * Mirrors the `nexora demo:load` CLI verb (T-006). The backend endpoint is
 * permission-gated on `platform.tenants.create_demo` (T-008 AC#1) — tenant
 * admins cannot invoke this against their own tenant, only platform
 * operators. The caller is responsible for hiding the trigger UI via
 * `usePermission('platform.tenants.create_demo')`.
 *
 * Synchronous on the backend today; the endpoint returns the full per-module
 * summary so the UI can render seeded / already-seeded / no-op / failed
 * for each module in one shot. If T-007 scenarios grow heavy enough to need
 * async seeding, swap the mutationFn for a polling hook under the same URL
 * without touching call sites.
 */
export function useCreateDemoEnvironment() {
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation<DemoSeedRunResult, Error, CreateDemoEnvironmentPayload>({
    mutationFn: async ({ tenantId, scenario }) => {
      const result = await api.post<DemoSeedRunResult>(
        '/identity/tenants/demo',
        { tenantId, scenario },
      );
      return result;
    },
    onSuccess: (data) => {
      const anyFailed = data.modules.some((m) => m.status === 'Failed');
      if (anyFailed) {
        toast.warning(t('lockey_platform_tenants_demo_completed_with_failures'));
      } else {
        toast.success(t('lockey_platform_tenants_demo_completed'));
      }
    },
    onError: (err) => handleApiError(err),
  });
}
