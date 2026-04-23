import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

interface ChangeEntry {
  field: string;
  old: unknown;
  new: unknown;
  entityType?: string;
  entityId?: string;
}

interface EntityDiffViewerProps {
  changes: string | null | undefined;
}

/**
 * Renders a human-readable field-level diff for an audit entry.
 *
 * Expected JSON shape (from `AuditLogBehavior.SerializeCapturedChanges`):
 *   [{ field, old, new, entityType?, entityId? }]
 *
 * When the audit entry touches a single entity, `entityType`/`entityId` are omitted and
 * all rows render in one table. When multiple entities were touched, rows are grouped by
 * `entityType`/`entityId` so the reviewer can see which entity a field belongs to.
 */
export function EntityDiffViewer({ changes }: EntityDiffViewerProps) {
  const { t } = useTranslation('audit');

  const parsed = useMemo<ChangeEntry[]>(() => {
    if (!changes) return [];
    try {
      const data: unknown = JSON.parse(changes);
      if (!Array.isArray(data)) return [];
      return data.filter(
        (item): item is ChangeEntry =>
          item !== null &&
          typeof item === 'object' &&
          'field' in item &&
          typeof (item as { field: unknown }).field === 'string',
      );
    } catch {
      return [];
    }
  }, [changes]);

  if (parsed.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        {t('lockey_audit_detail_no_changes')}
      </p>
    );
  }

  // Group by entity (type + id). Single-entity diffs resolve to a single group with null key.
  const groups = new Map<string, { entityType?: string; entityId?: string; rows: ChangeEntry[] }>();
  for (const entry of parsed) {
    const key = entry.entityType ? `${entry.entityType}:${entry.entityId ?? ''}` : '';
    const group = groups.get(key);
    if (group) {
      group.rows.push(entry);
    } else {
      groups.set(key, { entityType: entry.entityType, entityId: entry.entityId, rows: [entry] });
    }
  }

  return (
    <div className="space-y-4">
      {Array.from(groups.entries()).map(([mapKey, group]) => (
        <div key={mapKey || 'single'} className="space-y-2">
          {group.entityType && (
            <div className="text-sm font-medium">
              {group.entityType}
              {group.entityId && <span className="ms-2 text-muted-foreground">#{group.entityId}</span>}
            </div>
          )}
          <DiffTable rows={group.rows} t={t} />
        </div>
      ))}
    </div>
  );
}

interface DiffTableProps {
  rows: ChangeEntry[];
  t: ReturnType<typeof useTranslation>['t'];
}

function DiffTable({ rows, t }: DiffTableProps) {
  return (
    <div className="overflow-x-auto rounded-md border">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b bg-muted/50">
            <th className="px-4 py-2 text-start font-medium">{t('lockey_audit_col_field')}</th>
            <th className="px-4 py-2 text-start font-medium">{t('lockey_audit_col_old_value')}</th>
            <th className="px-4 py-2 text-start font-medium">{t('lockey_audit_col_new_value')}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((change) => (
            <tr key={change.field} className="border-b last:border-b-0">
              <td className="px-4 py-2 font-medium">{change.field}</td>
              <td className="px-4 py-2">
                <DiffValue value={change.old} variant="old" />
              </td>
              <td className="px-4 py-2">
                <DiffValue value={change.new} variant="new" />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Renders a single diff cell — null shows "—"; primitives render as strings; objects as compact JSON. */
function DiffValue({ value, variant }: { value: unknown; variant: 'old' | 'new' }) {
  if (value === null || value === undefined) {
    return <span className="text-muted-foreground">—</span>;
  }

  let text: string;
  try {
    text = typeof value === 'object' ? JSON.stringify(value) : String(value);
  } catch {
    text = '[unserializable]';
  }

  const cls =
    variant === 'old'
      ? 'bg-red-100 text-red-800 dark:bg-red-900/50 dark:text-red-200'
      : 'bg-green-100 text-green-800 dark:bg-green-900/50 dark:text-green-200';

  return (
    <span className={`inline-block max-w-[28rem] truncate rounded px-1.5 py-0.5 align-middle ${cls}`} title={text}>
      {text}
    </span>
  );
}
