import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { usePermissions } from '@/shared/hooks/usePermissions';

interface RequirePermissionProps {
  children: ReactNode;
  required: string[];
  mode?: 'all' | 'any';
  fallback?: ReactNode;
}

/**
 * Guard component that conditionally renders children based on permissions.
 * Empty required array explicitly allows access.
 */
export function RequirePermission({
  children,
  required,
  mode = 'all',
  fallback,
}: RequirePermissionProps) {
  const { t } = useTranslation();
  const { hasPermission, hasAnyPermission } = usePermissions();

  const hasAccess =
    required.length === 0 ||
    (mode === 'all'
      ? required.every((p) => hasPermission(p))
      : hasAnyPermission(required));

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
