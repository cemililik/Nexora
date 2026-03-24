import { type ReactNode, useEffect } from 'react';
import { useTranslation } from 'react-i18next';

import { usePermissions } from '@/shared/hooks/usePermissions';
import { useUiStore } from '@/shared/lib/stores/uiStore';

interface RequirePermissionProps {
  children: ReactNode;
  required: string[];
  mode?: 'all' | 'any';
  fallback?: ReactNode;
}

/**
 * Guard component that conditionally renders children based on permissions.
 * Empty required array explicitly allows access.
 * Clears breadcrumbs when access is denied so stale breadcrumbs from the
 * previous page are not displayed alongside the permission-denied message.
 */
export function RequirePermission({
  children,
  required,
  mode = 'all',
  fallback,
}: RequirePermissionProps) {
  const { t } = useTranslation();
  const { hasPermission, hasAnyPermission } = usePermissions();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);

  const hasAccess =
    required.length === 0 ||
    (mode === 'all'
      ? required.every((p) => hasPermission(p))
      : hasAnyPermission(required));

  useEffect(() => {
    if (!hasAccess) {
      setBreadcrumbs([]);
    }
  }, [hasAccess, setBreadcrumbs]);

  if (!hasAccess) {
    return (
      fallback ?? (
        <div className="flex items-center justify-center p-8 text-muted-foreground">
          {t('lockey_common_no_permission')}
        </div>
      )
    );
  }

  return <>{children}</>;
}
