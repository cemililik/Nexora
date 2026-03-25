import type { WidgetDataDto } from '../types';

interface TableWidgetProps {
  data: WidgetDataDto;
}

export function TableWidget({ data }: TableWidgetProps) {
  const rows = data.rows;
  if (rows.length === 0 || !rows[0]) return <p className="text-sm text-muted-foreground">No data</p>;

  const headers = Object.keys(rows[0]);

  return (
    <div className="overflow-auto max-h-64">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-border">
            {headers.map((h) => (
              <th key={h} className="px-2 py-1 text-left font-medium text-muted-foreground">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={i} className="border-b border-border/50">
              {headers.map((h) => (
                <td key={h} className="px-2 py-1 text-foreground">
                  {String(row[h] ?? '')}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
