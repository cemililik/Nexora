'use client';

import { Suspense, useMemo } from 'react';

import { useModules } from '@/shared/hooks/useModules';
import { usePermissions } from '@/shared/hooks/usePermissions';
import type { PortalSlotContribution } from '@/shared/types/module';

import { ErrorBoundary } from '../feedback/ErrorBoundary';
import { LoadingSkeleton } from '../feedback/LoadingSkeleton';

interface ModuleSlotProps {
  slotId: string;
  className?: string;
}

/**
 * Renders contributions from active modules into a named extension slot.
 *
 * Host pages invoke `<ModuleSlot slotId="contact.detail.aside" />` to expose
 * an extension point where other modules can inject widgets or panels.
 * Contributions are filtered by user permissions and sorted by order.
 * Each contribution is wrapped in ErrorBoundary for cross-module isolation.
 */
export function ModuleSlot({ slotId, className }: ModuleSlotProps) {
  const { activeModules } = useModules();
  const { hasPermission } = usePermissions();

  const contributions = useMemo<(PortalSlotContribution & { moduleName: string })[]>(
    () =>
      activeModules
        .flatMap((m) =>
          (m.slots?.[slotId] ?? [])
            .filter((c) => c.permissions.every((p) => hasPermission(p)))
            .map((c) => ({ ...c, moduleName: m.name })),
        )
        .sort((a, b) => a.order - b.order),
    [activeModules, slotId, hasPermission],
  );

  if (contributions.length === 0) return null;

  return (
    <div className={className}>
      {contributions.map((c) => (
        <ErrorBoundary key={`${c.moduleName}.${c.id}`}>
          <Suspense fallback={<LoadingSkeleton className="h-32" />}>
            <c.component />
          </Suspense>
        </ErrorBoundary>
      ))}
    </div>
  );
}
