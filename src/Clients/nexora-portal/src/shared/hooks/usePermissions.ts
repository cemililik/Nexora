'use client';

import { useAuthStore } from '@/shared/lib/stores/authStore';

/**
 * Hook for permission checking in portal components.
 * Permissions follow the format: module.resource.action
 */
export function usePermissions() {
  const { permissions, hasPermission, hasAnyPermission } = useAuthStore();

  return {
    permissions,
    hasPermission,
    hasAnyPermission,
  };
}
