import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

let mockPermissions: string[] = [];
vi.mock('@/shared/hooks/usePermissions', () => ({
  usePermissions: () => ({
    permissions: mockPermissions,
    hasPermission: (p: string) => mockPermissions.includes(p),
    hasAnyPermission: (perms: string[]) => perms.some((p) => mockPermissions.includes(p)),
  }),
}));

import { RequirePermission } from './RequirePermission';

describe('RequirePermission', () => {
  beforeEach(() => {
    mockPermissions = [];
  });

  it('should render children when user has required permissions', () => {
    mockPermissions = ['identity.users.read', 'identity.users.write'];

    render(
      <RequirePermission required={['identity.users.read']}>
        <div>Protected Content</div>
      </RequirePermission>,
    );

    expect(screen.getByText('Protected Content')).toBeInTheDocument();
  });

  it('should show fallback when user lacks permissions', () => {
    mockPermissions = ['identity.users.read'];

    render(
      <RequirePermission required={['identity.users.delete']}>
        <div>Protected Content</div>
      </RequirePermission>,
    );

    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
    expect(screen.getByText('lockey_common_no_permission')).toBeInTheDocument();
  });

  it('should allow access when required array is empty', () => {
    mockPermissions = [];

    render(
      <RequirePermission required={[]}>
        <div>Public Content</div>
      </RequirePermission>,
    );

    expect(screen.getByText('Public Content')).toBeInTheDocument();
  });

  it('should check any permission in "any" mode', () => {
    mockPermissions = ['identity.users.read'];

    render(
      <RequirePermission
        required={['identity.users.read', 'identity.users.write']}
        mode="any"
      >
        <div>Any Mode Content</div>
      </RequirePermission>,
    );

    expect(screen.getByText('Any Mode Content')).toBeInTheDocument();
  });

  it('should deny access in "any" mode when no permissions match', () => {
    mockPermissions = ['identity.users.read'];

    render(
      <RequirePermission
        required={['identity.roles.delete', 'identity.roles.write']}
        mode="any"
      >
        <div>Any Mode Content</div>
      </RequirePermission>,
    );

    expect(screen.queryByText('Any Mode Content')).not.toBeInTheDocument();
    expect(screen.getByText('lockey_common_no_permission')).toBeInTheDocument();
  });

  it('should show custom fallback when provided', () => {
    mockPermissions = [];

    render(
      <RequirePermission
        required={['admin.super']}
        fallback={<div>Custom Denied</div>}
      >
        <div>Protected Content</div>
      </RequirePermission>,
    );

    expect(screen.getByText('Custom Denied')).toBeInTheDocument();
  });
});
