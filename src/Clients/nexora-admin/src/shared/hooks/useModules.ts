import { useQuery } from '@tanstack/react-query';
import { useCallback, useMemo } from 'react';

import { api } from '@/shared/lib/api';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import { allAdminModules } from '@/modules/_registry';
import type { TenantModuleDto } from '@/shared/types/module';

const UUID_REGEX =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const moduleKeys = {
  installed: (tenantId: string) =>
    ['identity', 'modules', tenantId] as const,
};

/**
 * Fetches installed modules for the current tenant and filters
 * the admin module registry to only include active modules.
 */
export function useModules() {
  const tenantId = useAuthStore((s) => s.tenantId);

  const query = useQuery({
    queryKey: moduleKeys.installed(tenantId ?? ''),
    queryFn: () =>
      api.get<TenantModuleDto[]>(
        `/identity/tenants/${encodeURIComponent(tenantId!)}/modules`,
      ),
    enabled: !!tenantId && UUID_REGEX.test(tenantId),
    staleTime: 5 * 60 * 1000,
  });

  const installedModuleNames = useMemo(() => {
    if (!query.data) return new Set<string>();
    return new Set(
      query.data.filter((m) => m.isActive).map((m) => m.moduleName),
    );
  }, [query.data]);

  const activeModules = useMemo(
    () => allAdminModules.filter((m) => installedModuleNames.has(m.name)),
    [installedModuleNames],
  );

  const hasModule = useCallback(
    (moduleName: string): boolean => installedModuleNames.has(moduleName),
    [installedModuleNames],
  );

  return {
    activeModules,
    hasModule,
    isLoading: query.isPending,
  };
}
