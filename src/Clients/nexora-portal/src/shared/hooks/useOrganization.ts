'use client';

import { useQuery } from '@tanstack/react-query';

import { api } from '@/shared/lib/api';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import type { OrganizationBranding } from '@/shared/types/auth';

const organizationKeys = {
  detail: (id: string) => ['identity', 'organization', id] as const,
};

/**
 * Fetches and returns the current user's organization branding
 * (logo, default language, default currency, timezone).
 */
export function useOrganization() {
  const organizationId = useAuthStore((s) => s.organizationId);

  const query = useQuery({
    queryKey: organizationKeys.detail(organizationId ?? ''),
    queryFn: () =>
      api.get<OrganizationBranding>(
        `/identity/organizations/${organizationId}`,
      ),
    enabled: !!organizationId,
  });

  return {
    organization: query.data ?? null,
    isLoading: query.isLoading,
  };
}
