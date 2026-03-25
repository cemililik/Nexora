import { useTranslation } from 'react-i18next';
import type { TestReportQueryResultDto } from '../types';

interface SqlTestResultProps {
  result?: TestReportQueryResultDto;
  isPending: boolean;
}

export function SqlTestResult({ result, isPending }: SqlTestResultProps) {
  const { t } = useTranslation('reporting');

  if (isPending) {
    return <p className="text-sm text-muted-foreground">{t('lockey_reporting_loading')}</p>;
  }

  if (!result) return null;

  return (
    <div className="space-y-2">
      <p className="text-sm text-green-600">
        {t('lockey_reporting_test_query_success', { count: result.rowCount })}
      </p>
      {result.columns.length > 0 && (
        <div className="max-h-48 overflow-auto rounded-md border">
          <table className="w-full text-xs">
            <thead className="sticky top-0 bg-muted">
              <tr>
                {result.columns.map((col) => (
                  <th key={col} className="px-2 py-1 text-start font-medium text-muted-foreground">
                    {col}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {result.rows.map((row, i) => (
                <tr key={i} className="border-t">
                  {result.columns.map((col) => (
                    <td key={col} className="px-2 py-1 text-foreground">
                      {row[col] != null ? String(row[col]) : <span className="text-muted-foreground">null</span>}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
