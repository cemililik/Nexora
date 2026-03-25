import type { DashboardWidget } from '../types';
import { WidgetCard } from './WidgetCard';

interface DashboardGridProps {
  dashboardId: string;
  widgets: DashboardWidget[];
}

export function DashboardGrid({ dashboardId, widgets }: DashboardGridProps) {
  const maxCols = 3;

  return (
    <div
      className="grid gap-4"
      style={{
        gridTemplateColumns: `repeat(${maxCols}, 1fr)`,
      }}
    >
      {widgets.map((widget) => (
        <div
          key={widget.id}
          style={{
            gridColumn: `span ${Math.min(widget.sizeW, maxCols)}`,
            gridRow: `span ${widget.sizeH}`,
          }}
        >
          <WidgetCard dashboardId={dashboardId} widget={widget} />
        </div>
      ))}
    </div>
  );
}
