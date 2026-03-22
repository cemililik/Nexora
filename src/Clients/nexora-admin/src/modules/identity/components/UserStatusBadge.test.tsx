import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { UserStatusBadge, TenantStatusBadge } from './UserStatusBadge';
import type { UserStatus, TenantStatus } from '../types';

describe('UserStatusBadge', () => {
  it.each<{ status: UserStatus; expectedKey: string }>([
    { status: 'Active', expectedKey: 'lockey_identity_status_active' },
    { status: 'Inactive', expectedKey: 'lockey_identity_status_inactive' },
    { status: 'Locked', expectedKey: 'lockey_identity_status_locked' },
  ])('should render correct text for UserStatus "$status"', ({ status, expectedKey }) => {
    render(<UserStatusBadge status={status} />);

    expect(screen.getByText(expectedKey)).toBeInTheDocument();
  });

  it.each<{ status: UserStatus; expectedClass: string }>([
    { status: 'Active', expectedClass: 'bg-green-100' },
    { status: 'Inactive', expectedClass: 'bg-gray-100' },
    { status: 'Locked', expectedClass: 'bg-red-100' },
  ])('should apply correct styling for UserStatus "$status"', ({ status, expectedClass }) => {
    render(<UserStatusBadge status={status} />);

    const badge = screen.getByText(new RegExp(`lockey_identity_status_`));
    expect(badge.className).toContain(expectedClass);
  });
});

describe('TenantStatusBadge', () => {
  it.each<{ status: TenantStatus; expectedKey: string }>([
    { status: 'Trial', expectedKey: 'lockey_identity_status_trial' },
    { status: 'Active', expectedKey: 'lockey_identity_status_active' },
    { status: 'Suspended', expectedKey: 'lockey_identity_status_suspended' },
    { status: 'Terminated', expectedKey: 'lockey_identity_status_terminated' },
  ])('should render correct text for TenantStatus "$status"', ({ status, expectedKey }) => {
    render(<TenantStatusBadge status={status} />);

    expect(screen.getByText(expectedKey)).toBeInTheDocument();
  });

  it.each<{ status: TenantStatus; expectedClass: string }>([
    { status: 'Trial', expectedClass: 'bg-blue-100' },
    { status: 'Active', expectedClass: 'bg-green-100' },
    { status: 'Suspended', expectedClass: 'bg-yellow-100' },
    { status: 'Terminated', expectedClass: 'bg-red-100' },
  ])('should apply correct styling for TenantStatus "$status"', ({ status, expectedClass }) => {
    render(<TenantStatusBadge status={status} />);

    const badge = screen.getByText(new RegExp(`lockey_identity_status_`));
    expect(badge.className).toContain(expectedClass);
  });
});
