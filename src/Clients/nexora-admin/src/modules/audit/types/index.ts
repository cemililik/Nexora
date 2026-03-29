export interface AuditLogDto {
  id: string;
  module: string;
  operation: string;
  operationType: string;
  userEmail: string;
  isSuccess: boolean;
  entityType?: string;
  entityId?: string;
  timestamp: string;
}

export interface AuditLogDetailDto extends AuditLogDto {
  userId?: string;
  ipAddress?: string;
  userAgent?: string;
  correlationId?: string;
  errorKey?: string;
  beforeState?: string;
  afterState?: string;
  changes?: string;
  metadata?: string;
}

export interface AuditSettingDto {
  id: string;
  module: string;
  operation: string;
  isEnabled: boolean;
  retentionDays: number;
}

export interface AuditLogFilters {
  module?: string;
  operation?: string;
  userId?: string;
  entityType?: string;
  isSuccess?: boolean;
  dateFrom?: string;
  dateTo?: string;
}

export type AuditOperationType = 'Create' | 'Update' | 'Delete' | 'Action' | 'Read';

export type AuditSourceKind = 'Command' | 'Query';

export interface AuditableOperationDto {
  operation: string;
  operationType: string;
  sourceKind: AuditSourceKind;
}

export interface AuditableModuleDto {
  module: string;
  operations: AuditableOperationDto[];
}
