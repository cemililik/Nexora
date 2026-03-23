import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { ContactStatusBadge, ContactTypeBadge } from './ContactStatusBadge';
import type { ContactStatus, ContactType } from '../types';

describe('ContactStatusBadge', () => {
  it.each<{ status: ContactStatus; expectedKey: string }>([
    { status: 'Active', expectedKey: 'lockey_contacts_status_active' },
    { status: 'Archived', expectedKey: 'lockey_contacts_status_archived' },
    { status: 'Merged', expectedKey: 'lockey_contacts_status_merged' },
  ])('should render correct text for ContactStatus "$status"', ({ status, expectedKey }) => {
    render(<ContactStatusBadge status={status} />);

    expect(screen.getByText(expectedKey)).toBeInTheDocument();
  });

  it.each<{ status: ContactStatus; expectedClass: string }>([
    { status: 'Active', expectedClass: 'bg-green-100' },
    { status: 'Archived', expectedClass: 'bg-gray-100' },
    { status: 'Merged', expectedClass: 'bg-yellow-100' },
  ])('should apply correct styling for ContactStatus "$status"', ({ status, expectedClass }) => {
    render(<ContactStatusBadge status={status} />);

    const badge = screen.getByText(new RegExp('lockey_contacts_status_'));
    expect(badge.className).toContain(expectedClass);
  });
});

describe('ContactTypeBadge', () => {
  it.each<{ type: ContactType; expectedKey: string }>([
    { type: 'Individual', expectedKey: 'lockey_contacts_type_individual' },
    { type: 'Organization', expectedKey: 'lockey_contacts_type_organization' },
  ])('should render correct text for ContactType "$type"', ({ type, expectedKey }) => {
    render(<ContactTypeBadge type={type} />);

    expect(screen.getByText(expectedKey)).toBeInTheDocument();
  });

  it.each<{ type: ContactType; expectedClass: string }>([
    { type: 'Individual', expectedClass: 'bg-blue-100' },
    { type: 'Organization', expectedClass: 'bg-purple-100' },
  ])('should apply correct styling for ContactType "$type"', ({ type, expectedClass }) => {
    render(<ContactTypeBadge type={type} />);

    const badge = screen.getByText(new RegExp('lockey_contacts_type_'));
    expect(badge.className).toContain(expectedClass);
  });
});
