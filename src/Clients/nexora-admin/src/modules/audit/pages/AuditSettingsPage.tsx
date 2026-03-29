import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Input } from '@/shared/components/ui/input';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { RequirePermission } from '@/shared/components/guards/RequirePermission';
import { cn } from '@/shared/lib/utils';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useAuditSettings, useBulkUpdateAuditSettings } from '../hooks/useAuditSettings';
import { useAuditableOperations } from '../hooks/useAuditableOperations';
import { AuditOperationTypeBadge } from '../components/AuditOperationTypeBadge';
import type { AuditSettingDto } from '../types';

interface MergedOperation {
  module: string;
  operation: string;
  operationType: string;
  isEnabled: boolean;
  retentionDays: number;
  settingId: string | null;
}

// ── Toggle Switch ──────────────────────────────────────────────────────────────

interface ToggleSwitchProps {
  checked: boolean;
  indeterminate?: boolean;
  onChange: () => void;
  disabled?: boolean;
  size?: 'sm' | 'md';
}

function ToggleSwitch({ checked, indeterminate, onChange, disabled, size = 'md' }: ToggleSwitchProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (inputRef.current) {
      inputRef.current.indeterminate = indeterminate ?? false;
    }
  }, [indeterminate]);

  const sizeClasses = size === 'sm'
    ? 'h-4 w-7 after:h-3 after:w-3'
    : 'h-5 w-9 after:h-4 after:w-4';

  return (
    <label className="relative inline-flex shrink-0 cursor-pointer items-center">
      <input
        ref={inputRef}
        type="checkbox"
        checked={checked}
        onChange={onChange}
        disabled={disabled}
        className="peer sr-only"
      />
      <div
        className={cn(
          'peer rounded-full bg-muted after:absolute after:start-[2px] after:top-[2px] after:rounded-full after:bg-background after:transition-all after:content-[\'\'] peer-checked:bg-primary peer-checked:after:translate-x-full peer-focus:ring-2 peer-focus:ring-ring',
          sizeClasses,
          disabled && 'cursor-not-allowed opacity-50',
        )}
      />
    </label>
  );
}

// ── Compact Operation Item ─────────────────────────────────────────────────────

interface OperationItemProps {
  item: MergedOperation;
  onToggle: (module: string, operation: string) => void;
  isPending: boolean;
}

function OperationItem({ item, onToggle, isPending }: OperationItemProps) {
  const humanReadableName = item.operation
    .replace(/([A-Z])/g, ' $1')
    .trim();

  return (
    <div className="flex items-center gap-2 rounded-md border px-3 py-2">
      <ToggleSwitch
        checked={item.isEnabled}
        onChange={() => onToggle(item.module, item.operation)}
        disabled={isPending}
        size="sm"
      />
      <span className="text-sm leading-tight">{humanReadableName}</span>
      <AuditOperationTypeBadge operationType={item.operationType} />
    </div>
  );
}

// ── Collapsible Module Card ────────────────────────────────────────────────────

interface ModuleCardProps {
  moduleName: string;
  operations: MergedOperation[];
  isExpanded: boolean;
  onExpandToggle: () => void;
  onToggle: (module: string, operation: string) => void;
  onModuleToggle: (module: string, enabled: boolean) => void;
  isPending: boolean;
}

function ModuleCard({
  moduleName,
  operations,
  isExpanded,
  onExpandToggle,
  onToggle,
  onModuleToggle,
  isPending,
}: ModuleCardProps) {
  const { t } = useTranslation('audit');

  const enabledCount = operations.filter((o) => o.isEnabled).length;
  const totalCount = operations.length;
  const allEnabled = totalCount > 0 && enabledCount === totalCount;
  const someEnabled = enabledCount > 0 && !allEnabled;

  return (
    <Card>
      <CardHeader
        className="cursor-pointer select-none py-3"
        onClick={onExpandToggle}
      >
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            {/* Chevron */}
            <svg
              className={cn(
                'h-4 w-4 shrink-0 text-muted-foreground transition-transform duration-200',
                isExpanded && 'rotate-90',
              )}
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="m9 18 6-6-6-6" />
            </svg>
            <CardTitle className="text-base capitalize">{moduleName}</CardTitle>
            <Badge variant="secondary" className="text-xs tabular-nums">
              {enabledCount}/{totalCount} {t('lockey_audit_settings_operations_enabled')}
            </Badge>
          </div>
          <div
            className="flex items-center gap-2"
            onClick={(e) => e.stopPropagation()}
          >
            {totalCount > 0 && (
              <ToggleSwitch
                checked={allEnabled}
                indeterminate={someEnabled}
                onChange={() => onModuleToggle(moduleName, !allEnabled)}
                disabled={isPending}
              />
            )}
          </div>
        </div>
      </CardHeader>

      {isExpanded && (
        <CardContent className="pt-0 pb-4">
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {operations.map((op) => (
              <OperationItem
                key={`${op.module}-${op.operation}`}
                item={op}
                onToggle={onToggle}
                isPending={isPending}
              />
            ))}
          </div>
        </CardContent>
      )}
    </Card>
  );
}

// ── Main Page ──────────────────────────────────────────────────────────────────

const DEFAULT_RETENTION_DAYS = 90;

export default function AuditSettingsPage() {
  const { t } = useTranslation('audit');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('audit.settings.manage');

  const { data: auditableModules, isPending: isLoadingOperations } = useAuditableOperations();
  const { data: settings, isPending: isLoadingSettings } = useAuditSettings();
  const bulkUpdate = useBulkUpdateAuditSettings();

  const [localState, setLocalState] = useState<Map<string, MergedOperation>>(new Map());
  const [expandedModules, setExpandedModules] = useState<Set<string>>(new Set());
  const [searchQuery, setSearchQuery] = useState('');

  // Build a lookup key for merged operations
  const buildKey = useCallback((module: string, operation: string) => `${module}::${operation}`, []);

  // Merge discovered operations with saved settings
  useEffect(() => {
    if (!auditableModules) return;

    const settingLookup = new Map<string, AuditSettingDto>();
    if (settings) {
      for (const s of settings) {
        settingLookup.set(buildKey(s.module, s.operation), s);
      }
    }

    const merged = new Map<string, MergedOperation>();
    for (const mod of auditableModules) {
      for (const op of mod.operations) {
        const key = buildKey(mod.module, op.operation);
        const existing = settingLookup.get(key);
        merged.set(key, {
          module: mod.module,
          operation: op.operation,
          operationType: op.operationType,
          isEnabled: existing ? existing.isEnabled : true, // default: enabled
          retentionDays: existing ? existing.retentionDays : DEFAULT_RETENTION_DAYS,
          settingId: existing ? existing.id : null,
        });
      }
    }

    setLocalState(merged);

    // Expand the first two modules by default
    const moduleNames = [...new Set(auditableModules.map((m) => m.module))];
    setExpandedModules(new Set(moduleNames.slice(0, 2)));
  }, [auditableModules, settings, buildKey]);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_audit_module_name' },
      { label: 'lockey_audit_nav_settings' },
    ]);
  }, [setBreadcrumbs]);

  const groupedOperations = useMemo(() => {
    const groups = new Map<string, MergedOperation[]>();
    for (const op of localState.values()) {
      const existing = groups.get(op.module) ?? [];
      existing.push(op);
      groups.set(op.module, existing);
    }
    return groups;
  }, [localState]);

  // Filter modules and operations by search query
  const filteredGroups = useMemo(() => {
    if (!searchQuery.trim()) return groupedOperations;

    const query = searchQuery.toLowerCase();
    const filtered = new Map<string, MergedOperation[]>();

    for (const [moduleName, ops] of groupedOperations) {
      // Module name matches -> show all its operations
      if (moduleName.toLowerCase().includes(query)) {
        filtered.set(moduleName, ops);
        continue;
      }
      // Otherwise filter operations by name
      const matchingOps = ops.filter((op) => {
        const humanName = op.operation.replace(/([A-Z])/g, ' $1').trim().toLowerCase();
        return humanName.includes(query) || op.operation.toLowerCase().includes(query);
      });
      if (matchingOps.length > 0) {
        filtered.set(moduleName, matchingOps);
      }
    }
    return filtered;
  }, [groupedOperations, searchQuery]);

  // Count pending changes compared to saved settings
  const changeCount = useMemo(() => {
    if (!settings) return 0;

    const settingLookup = new Map<string, AuditSettingDto>();
    for (const s of settings) {
      settingLookup.set(buildKey(s.module, s.operation), s);
    }

    let count = 0;
    for (const item of localState.values()) {
      const key = buildKey(item.module, item.operation);
      const original = settingLookup.get(key);
      if (!original || original.isEnabled !== item.isEnabled || original.retentionDays !== item.retentionDays) {
        count++;
      }
    }
    return count;
  }, [localState, settings, buildKey]);

  const handleToggle = useCallback(
    (module: string, operation: string) => {
      const key = buildKey(module, operation);
      setLocalState((prev) => {
        const next = new Map(prev);
        const item = next.get(key);
        if (item) {
          next.set(key, { ...item, isEnabled: !item.isEnabled });
        }
        return next;
      });
    },
    [buildKey],
  );

  const handleModuleToggle = useCallback(
    (module: string, enabled: boolean) => {
      setLocalState((prev) => {
        const next = new Map(prev);
        for (const [key, item] of next) {
          if (item.module === module) {
            next.set(key, { ...item, isEnabled: enabled });
          }
        }
        return next;
      });
    },
    [],
  );

  const handleExpandToggle = useCallback((moduleName: string) => {
    setExpandedModules((prev) => {
      const next = new Set(prev);
      if (next.has(moduleName)) {
        next.delete(moduleName);
      } else {
        next.add(moduleName);
      }
      return next;
    });
  }, []);

  const handleSave = useCallback(() => {
    const settingLookup = new Map<string, AuditSettingDto>();
    if (settings) {
      for (const s of settings) {
        settingLookup.set(buildKey(s.module, s.operation), s);
      }
    }

    const changedItems: Array<{ module: string; operation: string; isEnabled: boolean; retentionDays: number }> = [];

    for (const item of localState.values()) {
      const key = buildKey(item.module, item.operation);
      const original = settingLookup.get(key);

      const hasChanged =
        !original ||
        original.isEnabled !== item.isEnabled ||
        original.retentionDays !== item.retentionDays;

      if (hasChanged) {
        changedItems.push({
          module: item.module,
          operation: item.operation,
          isEnabled: item.isEnabled,
          retentionDays: item.retentionDays,
        });
      }
    }

    if (changedItems.length > 0) {
      bulkUpdate.mutate(changedItems);
    }
  }, [localState, settings, buildKey, bulkUpdate]);

  if (isLoadingOperations || isLoadingSettings) return <LoadingSkeleton lines={6} />;

  return (
    <RequirePermission required={['audit.settings.read']}>
      <div className="space-y-4 pb-20">
        {/* Page Header */}
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_audit_settings_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_audit_settings_description')}
          </p>
        </div>

        {/* Search Filter */}
        <div className="max-w-sm">
          <Input
            type="text"
            placeholder={t('lockey_audit_settings_search_placeholder')}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="h-9"
          />
        </div>

        {filteredGroups.size === 0 && (
          <p className="text-sm text-muted-foreground">
            {searchQuery
              ? t('lockey_audit_settings_no_search_results')
              : t('lockey_audit_settings_no_operations')}
          </p>
        )}

        {/* Module Accordion Cards */}
        {Array.from(filteredGroups.entries()).map(([moduleName, ops]) => (
          <ModuleCard
            key={moduleName}
            moduleName={moduleName}
            operations={ops}
            isExpanded={expandedModules.has(moduleName) || searchQuery.trim().length > 0}
            onExpandToggle={() => handleExpandToggle(moduleName)}
            onToggle={handleToggle}
            onModuleToggle={handleModuleToggle}
            isPending={bulkUpdate.isPending || !canManage}
          />
        ))}
      </div>

      {/* Sticky Save Bar */}
      <div className="fixed inset-x-0 bottom-0 z-40 border-t bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/80">
        <div className="mx-auto flex max-w-screen-xl items-center justify-between px-6 py-3">
          <span className="text-sm text-muted-foreground">
            {changeCount > 0
              ? t('lockey_audit_settings_unsaved_changes', { count: changeCount })
              : t('lockey_audit_settings_no_changes')}
          </span>
          <Button
            type="button"
            onClick={handleSave}
            disabled={bulkUpdate.isPending || changeCount === 0 || !canManage}
          >
            {bulkUpdate.isPending
              ? t('lockey_audit_settings_saving')
              : t('lockey_audit_settings_save')}
          </Button>
        </div>
      </div>
    </RequirePermission>
  );
}
