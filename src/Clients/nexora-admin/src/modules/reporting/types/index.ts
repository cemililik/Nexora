export type ReportFormat = 'Csv' | 'Excel' | 'Pdf' | 'Json';
export type ReportStatus = 'Queued' | 'Running' | 'Completed' | 'Failed';
export type WidgetType = 'Chart' | 'Kpi' | 'Table';
export type ChartType = 'Bar' | 'Line' | 'Pie' | 'Area';

export interface ReportDefinitionDto {
  id: string;
  name: string;
  description?: string;
  module: string;
  category?: string;
  queryText: string;
  parameters?: string;
  defaultFormat: string;
  isActive: boolean;
  createdAt: string;
  createdBy?: string;
}

export interface ReportExecutionDto {
  id: string;
  definitionId: string;
  status: string;
  parameterValues?: string;
  format: string;
  rowCount?: number;
  durationMs?: number;
  errorDetails?: string;
  executedBy?: string;
  createdAt: string;
}

export interface ReportScheduleDto {
  id: string;
  definitionId: string;
  cronExpression: string;
  format: string;
  recipients?: string;
  isActive: boolean;
  lastExecutionAt?: string;
  nextExecutionAt?: string;
  createdAt: string;
}

export interface DashboardDto {
  id: string;
  name: string;
  description?: string;
  isDefault: boolean;
  widgets?: string;
  createdAt: string;
  createdBy?: string;
}

export interface DashboardWidget {
  id: string;
  type: WidgetType;
  title: string;
  reportDefinitionId: string;
  chartType?: ChartType;
  positionX: number;
  positionY: number;
  sizeW: number;
  sizeH: number;
  config?: string;
}

export interface WidgetDataDto {
  widgetId: string;
  widgetType: string;
  rows: Record<string, unknown>[];
  rowCount: number;
}

export interface CreateReportDefinitionRequest {
  name: string;
  description?: string;
  module: string;
  category?: string;
  queryText: string;
  parameters?: string;
  defaultFormat: string;
}

export interface UpdateReportDefinitionRequest extends CreateReportDefinitionRequest {
  id: string;
}

export interface ExecuteReportRequest {
  definitionId: string;
  format?: string;
  parameterValues?: string;
}

export interface CreateReportScheduleRequest {
  definitionId: string;
  cronExpression: string;
  format: string;
  recipients?: string;
}

export interface CreateDashboardRequest {
  name: string;
  description?: string;
  isDefault: boolean;
}

export interface UpdateDashboardRequest {
  id: string;
  name: string;
  description?: string;
  widgets?: string;
  isDefault: boolean;
}

export interface DownloadReportResultDto {
  url: string;
  expiresAt: string;
}
