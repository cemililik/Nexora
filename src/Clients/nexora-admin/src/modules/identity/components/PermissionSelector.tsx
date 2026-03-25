import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

import { usePermissions } from '../hooks/useRoles';
import type { PermissionDto } from '../types';

interface PermissionSelectorProps {
  selectedIds: string[];
  onChange: (ids: string[]) => void;
  disabled?: boolean;
}

/**
 * Permission checkbox grid grouped by module.
 * Used in role create and edit forms.
 */
export function PermissionSelector({ selectedIds, onChange, disabled }: PermissionSelectorProps) {
  const { t } = useTranslation('identity');
  const { data: permissions, isLoading } = usePermissions();

  const grouped = useMemo(() => {
    if (!permissions) return {};
    return permissions.reduce<Record<string, PermissionDto[]>>((acc, p) => {
      (acc[p.module] ??= []).push(p);
      return acc;
    }, {});
  }, [permissions]);

  const selectedSet = useMemo(() => new Set(selectedIds), [selectedIds]);

  const togglePermission = (id: string) => {
    if (selectedSet.has(id)) {
      onChange(selectedIds.filter((s) => s !== id));
    } else {
      onChange([...selectedIds, id]);
    }
  };

  const toggleModule = (modulePerms: PermissionDto[]) => {
    const moduleIds = modulePerms.map((p) => p.id);
    const allSelected = moduleIds.every((id) => selectedSet.has(id));

    if (allSelected) {
      onChange(selectedIds.filter((id) => !moduleIds.includes(id)));
    } else {
      const newIds = new Set(selectedIds);
      moduleIds.forEach((id) => newIds.add(id));
      onChange([...newIds]);
    }
  };

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">{t('lockey_identity_loading')}</p>;
  }

  const modules = Object.entries(grouped).sort(([a], [b]) => a.localeCompare(b));

  return (
    <div className="space-y-4 max-h-80 overflow-y-auto">
      {modules.map(([module, perms]) => {
        const moduleIds = perms.map((p) => p.id);
        const allChecked = moduleIds.every((id) => selectedSet.has(id));
        const someChecked = !allChecked && moduleIds.some((id) => selectedSet.has(id));

        return (
          <div key={module} className="space-y-1">
            <label className="flex items-center gap-2 text-sm font-medium cursor-pointer">
              <input
                type="checkbox"
                checked={allChecked}
                ref={(el) => { if (el) el.indeterminate = someChecked; }}
                onChange={() => toggleModule(perms)}
                disabled={disabled}
                className="h-4 w-4 rounded border-input"
              />
              <span className="capitalize">{module}</span>
              <span className="text-xs text-muted-foreground">
                ({moduleIds.filter((id) => selectedSet.has(id)).length}/{moduleIds.length})
              </span>
            </label>
            <div className="ms-6 grid gap-1 sm:grid-cols-2">
              {perms.map((p) => (
                <label key={p.id} className="flex items-center gap-2 text-sm cursor-pointer text-muted-foreground hover:text-foreground">
                  <input
                    type="checkbox"
                    checked={selectedSet.has(p.id)}
                    onChange={() => togglePermission(p.id)}
                    disabled={disabled}
                    className="h-3.5 w-3.5 rounded border-input"
                  />
                  <span>{p.resource}.{p.action}</span>
                </label>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}
