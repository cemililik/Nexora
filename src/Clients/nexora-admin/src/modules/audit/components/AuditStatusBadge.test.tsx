import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { AuditStatusBadge } from './AuditStatusBadge';

describe('AuditStatusBadge', () => {
  it('should render success badge when isSuccess is true', () => {
    render(<AuditStatusBadge isSuccess />);

    expect(screen.getByText('lockey_audit_status_success')).toBeInTheDocument();
  });

  it('should render failed badge when isSuccess is false', () => {
    render(<AuditStatusBadge isSuccess={false} />);

    expect(screen.getByText('lockey_audit_status_failed')).toBeInTheDocument();
  });

  it('should apply green classes for success', () => {
    render(<AuditStatusBadge isSuccess />);

    const badge = screen.getByText('lockey_audit_status_success');
    expect(badge.className).toContain('bg-green-100');
    expect(badge.className).toContain('text-green-800');
  });

  it('should apply red classes for failure', () => {
    render(<AuditStatusBadge isSuccess={false} />);

    const badge = screen.getByText('lockey_audit_status_failed');
    expect(badge.className).toContain('bg-red-100');
    expect(badge.className).toContain('text-red-800');
  });
});
