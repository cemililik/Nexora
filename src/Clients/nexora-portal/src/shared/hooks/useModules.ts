'use client';

import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import { api } from '@/shared/lib/api';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import { allPortalModules } from '@/modules/_registry';
import type { TenantModuleDto } from '@/shared/types/module';

const moduleKeys = {
  installed: (tenantId: string) =>
    ['identity', 'modules', tenantId] as const,
};

/**
 * Fetches installed modules for the current tenant and filters
 * the portal module registry to only include active modules.
 */
export function useModules() {
  const tenantId = useAuthStore((s) => s.tenantId);

  const query = useQuery({
    queryKey: moduleKeys.installed(tenantId ?? ''),
    queryFn: () =>
      api.get<TenantModuleDto[]>(
        `/identity/tenants/${tenantId}/modules`,
      ),
    enabled: !!tenantId,
    staleTime: 5 * 60 * 1000, // Modules change rarely
  });

  const installedModuleNames = useMemo(() => {
    if (!query.data) return new Set<string>();
    return new Set(
      query.data.filter((m) => m.isActive).map((m) => m.moduleName),
    );
  }, [query.data]);

  const activeModules = useMemo(
    () =>
      allPortalModules.filter((m) => installedModuleNames.has(m.name)),
    [installedModuleNames],
  );

  const hasModule = (moduleName: string): boolean =>
    installedModuleNames.has(moduleName);

  return {
    activeModules,
    hasModule,
    isLoading: query.isLoading,
    installedModuleNames,
  };
}
