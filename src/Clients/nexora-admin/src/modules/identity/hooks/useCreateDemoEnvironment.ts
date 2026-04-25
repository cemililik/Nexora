import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import { tenantKeys } from './useTenants';
import { moduleKeys } from './useModuleManagement';

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
  /**
   * Optional cancellation signal — wired through to the underlying axios
   * request. Lets the caller (e.g. a dialog) abort an in-flight mutation
   * on close so a delayed onSuccess does not paint stale state into a
   * dialog that has already been dismissed and re-opened.
   */
  signal?: AbortSignal;
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
 *
 * On success invalidates `tenantKeys.detail(tenantId)` and
 * `moduleKeys.all(tenantId)` so `useTenant` / `useTenantModules` on the
 * caller page (e.g. TenantDetailPage) re-fetch — demo seed writes into the
 * tenant schema and module-marker rows, both of which the detail view
 * surfaces.
 */
export function useCreateDemoEnvironment() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation<DemoSeedRunResult, Error, CreateDemoEnvironmentPayload>({
    mutationFn: async ({ tenantId, scenario, signal }) => {
      const result = await api.post<DemoSeedRunResult>(
        '/identity/tenants/demo',
        { tenantId, scenario },
        signal ? { signal } : undefined,
      );
      return result;
    },
    onSuccess: (data, variables) => {
      const anyFailed = data.modules.some((m) => m.status === 'Failed');
      if (anyFailed) {
        toast.warning(t('lockey_identity_tenants_demo_completed_with_failures'));
      } else {
        toast.success(t('lockey_identity_tenants_demo_completed'));
      }
      void queryClient.invalidateQueries({
        queryKey: tenantKeys.detail(variables.tenantId),
      });
      void queryClient.invalidateQueries({
        queryKey: moduleKeys.all(variables.tenantId),
      });
    },
    // Keep the (err) => handleApiError(err) wrapper: `handleApiError`'s
    // signature is (error, setError?) for form integration, and passing
    // it directly to TanStack's onError (which receives (error, variables,
    // context)) causes a TS2322 under strict mode because the second
    // arg types collide.
    onError: (err) => handleApiError(err),
  });
}
