import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import type { ChartType, WidgetDataDto } from '../types';

const COLORS = ['#8884d8', '#82ca9d', '#ffc658', '#ff7300', '#0088fe', '#00c49f'];

interface ChartWidgetProps {
  data: WidgetDataDto;
  chartType: ChartType;
}

export function ChartWidget({ data, chartType }: ChartWidgetProps) {
  const rows = data.rows;
  if (rows.length === 0 || !rows[0]) return <p className="text-sm text-muted-foreground">No data</p>;

  const keys = Object.keys(rows[0]);
  const xKey = keys[0];
  const valueKeys = keys.slice(1);

  return (
    <ResponsiveContainer width="100%" height={250}>
      {chartType === 'Bar' ? (
        <BarChart data={rows}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey={xKey} fontSize={12} />
          <YAxis fontSize={12} />
          <Tooltip />
          <Legend />
          {valueKeys.map((key, i) => (
            <Bar key={key} dataKey={key} fill={COLORS[i % COLORS.length]} />
          ))}
        </BarChart>
      ) : chartType === 'Line' ? (
        <LineChart data={rows}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey={xKey} fontSize={12} />
          <YAxis fontSize={12} />
          <Tooltip />
          <Legend />
          {valueKeys.map((key, i) => (
            <Line key={key} type="monotone" dataKey={key} stroke={COLORS[i % COLORS.length]} />
          ))}
        </LineChart>
      ) : chartType === 'Area' ? (
        <AreaChart data={rows}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey={xKey} fontSize={12} />
          <YAxis fontSize={12} />
          <Tooltip />
          <Legend />
          {valueKeys.map((key, i) => (
            <Area
              key={key}
              type="monotone"
              dataKey={key}
              fill={COLORS[i % COLORS.length]}
              stroke={COLORS[i % COLORS.length]}
              fillOpacity={0.3}
            />
          ))}
        </AreaChart>
      ) : (
        <PieChart>
          <Pie
            data={rows}
            dataKey={valueKeys[0]}
            nameKey={xKey}
            cx="50%"
            cy="50%"
            outerRadius={80}
            label
          >
            {rows.map((_, i) => (
              <Cell key={`cell-${i}`} fill={COLORS[i % COLORS.length]} />
            ))}
          </Pie>
          <Tooltip />
          <Legend />
        </PieChart>
      )}
    </ResponsiveContainer>
  );
}
