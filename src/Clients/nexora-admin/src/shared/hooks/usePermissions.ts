import { useCallback } from 'react';

import { useAuthStore } from '@/shared/lib/stores/authStore';

/**
 * Hook for permission checking in admin components.
 * Permissions follow the format: module.resource.action
 */
export function usePermissions() {
  const permissions = useAuthStore((s) => s.permissions);

  const hasPermission = useCallback(
    (permission: string): boolean => permissions.includes(permission),
    [permissions],
  );

  const hasAnyPermission = useCallback(
    (perms: string[]): boolean => perms.some((p) => permissions.includes(p)),
    [permissions],
  );

  return {
    permissions,
    hasPermission,
    hasAnyPermission,
  };
}
