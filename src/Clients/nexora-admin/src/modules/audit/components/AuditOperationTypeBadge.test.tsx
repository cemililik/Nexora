import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { AuditOperationTypeBadge } from './AuditOperationTypeBadge';

describe('AuditOperationTypeBadge', () => {
  it('should render Create badge with correct translation key', () => {
    render(<AuditOperationTypeBadge operationType="Create" />);

    expect(screen.getByText('lockey_audit_type_create')).toBeInTheDocument();
  });

  it('should render Update badge with correct translation key', () => {
    render(<AuditOperationTypeBadge operationType="Update" />);

    expect(screen.getByText('lockey_audit_type_update')).toBeInTheDocument();
  });

  it('should render Delete badge with correct translation key', () => {
    render(<AuditOperationTypeBadge operationType="Delete" />);

    expect(screen.getByText('lockey_audit_type_delete')).toBeInTheDocument();
  });

  it('should render Action badge with correct translation key', () => {
    render(<AuditOperationTypeBadge operationType="Action" />);

    expect(screen.getByText('lockey_audit_type_action')).toBeInTheDocument();
  });

  it('should render Read badge with correct translation key', () => {
    render(<AuditOperationTypeBadge operationType="Read" />);

    expect(screen.getByText('lockey_audit_type_read')).toBeInTheDocument();
  });

  it('should render raw operationType for unknown types', () => {
    render(<AuditOperationTypeBadge operationType="Custom" />);

    expect(screen.getByText('Custom')).toBeInTheDocument();
  });

  it('should apply green classes for Create', () => {
    render(<AuditOperationTypeBadge operationType="Create" />);

    const badge = screen.getByText('lockey_audit_type_create');
    expect(badge.className).toContain('bg-green-100');
  });

  it('should apply blue classes for Update', () => {
    render(<AuditOperationTypeBadge operationType="Update" />);

    const badge = screen.getByText('lockey_audit_type_update');
    expect(badge.className).toContain('bg-blue-100');
  });

  it('should apply red classes for Delete', () => {
    render(<AuditOperationTypeBadge operationType="Delete" />);

    const badge = screen.getByText('lockey_audit_type_delete');
    expect(badge.className).toContain('bg-red-100');
  });

  it('should apply fallback classes for unknown types', () => {
    render(<AuditOperationTypeBadge operationType="Unknown" />);

    const badge = screen.getByText('Unknown');
    expect(badge.className).toContain('bg-secondary');
  });
});
