'use client';

import { Suspense, useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';

import { useModules } from '@/shared/hooks/useModules';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { cn } from '@/shared/lib/utils';
import type { PortalSlotContribution } from '@/shared/types/module';

import { ErrorBoundary } from '../feedback/ErrorBoundary';
import { LoadingSkeleton } from '../feedback/LoadingSkeleton';

interface ModuleTabsProps {
  slotId: string;
  /** i18n namespace used to resolve each contribution's `labelKey`. */
  translationNamespace?: string;
  /** Optional built-in tabs rendered before module contributions. */
  builtinTabs?: BuiltinTab[];
  className?: string;
}

export interface BuiltinTab {
  id: string;
  labelKey: string;
  render: () => React.ReactNode;
}

/**
 * Renders a tabbed extension point. Host pages expose a slot id; modules
 * contribute labeled tabs via their manifest `slots[slotId]`.
 *
 * Built-in tabs (owned by the host page) render first; module contributions
 * follow in `order`. Tab labels resolve via `translationNamespace`.
 *
 * Matches UX_UI_STANDARDS §3 underline-tab pattern — state via `useState`,
 * no Radix/shadcn tabs, max 5 tabs per page (host enforces this).
 */
export function ModuleTabs({
  slotId,
  translationNamespace,
  builtinTabs = [],
  className,
}: ModuleTabsProps) {
  const { activeModules } = useModules();
  const { hasPermission } = usePermissions();
  const t = useTranslations(translationNamespace);

  const moduleTabs = useMemo<(PortalSlotContribution & { moduleName: string })[]>(
    () =>
      activeModules
        .flatMap((m) =>
          (m.slots?.[slotId] ?? [])
            .filter((c) => c.permissions.every((p) => hasPermission(p)))
            .filter((c) => !!c.labelKey)
            .map((c) => ({ ...c, moduleName: m.name })),
        )
        .sort((a, b) => a.order - b.order),
    [activeModules, slotId, hasPermission],
  );

  const tabIds = useMemo(
    () => [...builtinTabs.map((b) => b.id), ...moduleTabs.map((m) => `${m.moduleName}.${m.id}`)],
    [builtinTabs, moduleTabs],
  );

  const [activeId, setActiveId] = useState<string | undefined>(tabIds[0]);

  if (tabIds.length === 0) return null;

  const current = activeId ?? tabIds[0];

  return (
    <div className={className}>
      <div role="tablist" className="flex border-b border-border">
        {builtinTabs.map((b) => (
          <TabButton
            key={b.id}
            id={b.id}
            label={t(b.labelKey)}
            active={current === b.id}
            onSelect={setActiveId}
          />
        ))}
        {moduleTabs.map((m) => {
          const id = `${m.moduleName}.${m.id}`;
          return (
            <TabButton
              key={id}
              id={id}
              label={t(m.labelKey!)}
              active={current === id}
              onSelect={setActiveId}
            />
          );
        })}
      </div>

      <div role="tabpanel" className="pt-4">
        {builtinTabs.find((b) => b.id === current)?.render()}
        {moduleTabs.map((m) => {
          const id = `${m.moduleName}.${m.id}`;
          if (id !== current) return null;
          return (
            <ErrorBoundary key={id}>
              <Suspense fallback={<LoadingSkeleton className="h-32" />}>
                <m.component />
              </Suspense>
            </ErrorBoundary>
          );
        })}
      </div>
    </div>
  );
}

interface TabButtonProps {
  id: string;
  label: string;
  active: boolean;
  onSelect: (id: string) => void;
}

function TabButton({ id, label, active, onSelect }: TabButtonProps) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={() => onSelect(id)}
      className={cn(
        '-mb-px border-b-2 px-4 py-2 text-sm transition-colors',
        active
          ? 'border-primary font-medium text-foreground'
          : 'border-transparent text-muted-foreground hover:text-foreground',
      )}
    >
      {label}
    </button>
  );
}
