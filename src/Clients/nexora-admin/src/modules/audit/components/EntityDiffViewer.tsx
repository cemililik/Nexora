import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

interface ChangeEntry {
  field: string;
  old: string | null;
  new: string | null;
}

interface EntityDiffViewerProps {
  changes: string | null | undefined;
}

export function EntityDiffViewer({ changes }: EntityDiffViewerProps) {
  const { t } = useTranslation('audit');

  const parsedChanges = useMemo<ChangeEntry[]>(() => {
    if (!changes) return [];
    try {
      const parsed: unknown = JSON.parse(changes);
      if (!Array.isArray(parsed)) return [];
      return parsed.filter(
        (item): item is ChangeEntry =>
          item !== null &&
          typeof item === 'object' &&
          'field' in item &&
          typeof item.field === 'string' &&
          'old' in item &&
          'new' in item,
      );
    } catch {
      return [];
    }
  }, [changes]);

  if (parsedChanges.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        {t('lockey_audit_detail_no_changes')}
      </p>
    );
  }

  return (
    <div className="overflow-x-auto rounded-md border">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b bg-muted/50">
            <th className="px-4 py-2 text-start font-medium">
              {t('lockey_audit_col_field')}
            </th>
            <th className="px-4 py-2 text-start font-medium">
              {t('lockey_audit_col_old_value')}
            </th>
            <th className="px-4 py-2 text-start font-medium">
              {t('lockey_audit_col_new_value')}
            </th>
          </tr>
        </thead>
        <tbody>
          {parsedChanges.map((change) => (
            <tr key={change.field} className="border-b last:border-b-0">
              <td className="px-4 py-2 font-medium">{change.field}</td>
              <td className="px-4 py-2">
                <span className="rounded bg-red-100 px-1.5 py-0.5 text-red-800 dark:bg-red-900/50 dark:text-red-200">
                  {change.old ?? '—'}
                </span>
              </td>
              <td className="px-4 py-2">
                <span className="rounded bg-green-100 px-1.5 py-0.5 text-green-800 dark:bg-green-900/50 dark:text-green-200">
                  {change.new ?? '—'}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
