import type { WidgetDataDto } from '../types';

interface KpiWidgetProps {
  data: WidgetDataDto;
}

export function KpiWidget({ data }: KpiWidgetProps) {
  const row = data.rows[0];
  if (!row) return <p className="text-sm text-muted-foreground">No data</p>;

  const keys = Object.keys(row);
  const primaryKey = keys[0];
  const secondaryKey = keys[1];
  const value = primaryKey ? row[primaryKey] : undefined;

  return (
    <div className="flex flex-col items-center justify-center py-4">
      <span className="text-4xl font-bold text-foreground">
        {String(value ?? '\u2014')}
      </span>
      {secondaryKey && (
        <span className="mt-1 text-sm text-muted-foreground">
          {String(row[secondaryKey] ?? '')}
        </span>
      )}
    </div>
  );
}
