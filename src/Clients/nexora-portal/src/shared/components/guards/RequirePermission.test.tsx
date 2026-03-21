import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { RequirePermission } from './RequirePermission';

// Mock next-intl
vi.mock('next-intl', () => ({
  useTranslations: () => (key: string) => key,
}));

// Mock usePermissions
const mockHasPermission = vi.fn();
const mockHasAnyPermission = vi.fn();

vi.mock('@/shared/hooks/usePermissions', () => ({
  usePermissions: () => ({
    hasPermission: mockHasPermission,
    hasAnyPermission: mockHasAnyPermission,
  }),
}));

describe('RequirePermission', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render children when user has all required permissions', () => {
    mockHasPermission.mockReturnValue(true);

    render(
      <RequirePermission permissions={['donations.read', 'donations.write']}>
        <div>Protected Content</div>
      </RequirePermission>,
    );

    expect(screen.getByText('Protected Content')).toBeInTheDocument();
  });

  it('should show fallback when user lacks permissions in "all" mode', () => {
    mockHasPermission.mockImplementation(
      (p: string) => p === 'donations.read',
    );

    render(
      <RequirePermission permissions={['donations.read', 'donations.write']}>
        <div>Protected Content</div>
      </RequirePermission>,
    );

    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
    expect(
      screen.getByText('lockey_common_module_no_permission'),
    ).toBeInTheDocument();
  });

  it('should render children when user has any permission in "any" mode', () => {
    mockHasPermission.mockReturnValue(false);
    mockHasAnyPermission.mockReturnValue(true);

    render(
      <RequirePermission
        permissions={['donations.read', 'contacts.read']}
        mode="any"
      >
        <div>Protected Content</div>
      </RequirePermission>,
    );

    expect(screen.getByText('Protected Content')).toBeInTheDocument();
  });

  it('should show default fallback when no permissions in "any" mode', () => {
    mockHasPermission.mockReturnValue(false);
    mockHasAnyPermission.mockReturnValue(false);

    render(
      <RequirePermission
        permissions={['donations.read']}
        mode="any"
      >
        <div>Protected Content</div>
      </RequirePermission>,
    );

    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
    expect(
      screen.getByText('lockey_common_module_no_permission'),
    ).toBeInTheDocument();
  });

  it('should render custom fallback when provided', () => {
    mockHasPermission.mockReturnValue(false);

    render(
      <RequirePermission
        permissions={['admin.manage']}
        fallback={<div>Custom Denied</div>}
      >
        <div>Protected Content</div>
      </RequirePermission>,
    );

    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
    expect(screen.getByText('Custom Denied')).toBeInTheDocument();
  });
});
