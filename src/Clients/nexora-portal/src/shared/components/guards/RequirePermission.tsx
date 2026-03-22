'use client';

import { useTranslations } from 'next-intl';
import { type ReactNode } from 'react';

import { usePermissions } from '@/shared/hooks/usePermissions';

interface RequirePermissionProps {
  permissions: string[];
  /** If true, user must have ALL listed permissions. Default: all. */
  mode?: 'all' | 'any';
  children: ReactNode;
  fallback?: ReactNode;
}

/**
 * Guard component that renders children only if the user has the required permissions.
 * Shows a translated "no permission" message as default fallback.
 */
export function RequirePermission({
  permissions: required,
  mode = 'all',
  children,
  fallback,
}: RequirePermissionProps) {
  const { hasPermission, hasAnyPermission } = usePermissions();
  const t = useTranslations();

  const hasAccess =
    required.length === 0 ||
    (mode === 'all'
      ? required.every((p) => hasPermission(p))
      : hasAnyPermission(required));

  if (!hasAccess) {
    return (
      fallback ?? (
        <div className="flex items-center justify-center p-8 text-muted-foreground">
          <p>{t('lockey_common_module_no_permission')}</p>
        </div>
      )
    );
  }

  return <>{children}</>;
}
