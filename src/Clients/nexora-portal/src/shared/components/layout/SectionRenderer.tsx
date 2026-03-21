'use client';

import { Suspense, useMemo } from 'react';

import { useModules } from '@/shared/hooks/useModules';
import { usePermissions } from '@/shared/hooks/usePermissions';
import type { SectionPosition } from '@/shared/types/module';

import { LoadingSkeleton } from '../feedback/LoadingSkeleton';

interface SectionRendererProps {
  position: SectionPosition;
  className?: string;
}

/**
 * Renders module-contributed sections for a given position slot.
 * Filters by installed modules and user permissions, sorts by order.
 *
 * This is the core of the page builder infrastructure: modules register
 * sections in their manifests, and this component composites them
 * into the correct page slots.
 */
export function SectionRenderer({ position, className }: SectionRendererProps) {
  const { activeModules } = useModules();
  const { hasPermission } = usePermissions();

  const sections = useMemo(() => {
    return activeModules
      .flatMap((m) =>
        (m.sections ?? [])
          .filter((s) => s.position === position)
          .filter((s) => s.permissions.every((p) => hasPermission(p)))
          .map((s) => ({ ...s, moduleName: m.name })),
      )
      .sort((a, b) => a.order - b.order);
  }, [activeModules, position, hasPermission]);

  if (sections.length === 0) return null;

  return (
    <div className={className}>
      {sections.map((section) => (
        <Suspense
          key={section.id}
          fallback={<LoadingSkeleton className="h-32" />}
        >
          <section.component />
        </Suspense>
      ))}
    </div>
  );
}
