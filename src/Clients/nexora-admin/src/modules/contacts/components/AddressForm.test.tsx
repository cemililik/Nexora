import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { AddressForm } from './AddressForm';

describe('AddressForm', () => {
  it('should render all address fields', () => {
    render(
      <AddressForm onSubmit={vi.fn()} isPending={false} />,
    );

    expect(screen.getByLabelText('lockey_contacts_address_type')).toBeInTheDocument();
    expect(screen.getByLabelText('lockey_contacts_address_street1')).toBeInTheDocument();
    expect(screen.getByLabelText('lockey_contacts_address_street2')).toBeInTheDocument();
    expect(screen.getByLabelText('lockey_contacts_address_city')).toBeInTheDocument();
    expect(screen.getByLabelText('lockey_contacts_address_state')).toBeInTheDocument();
    expect(screen.getByLabelText('lockey_contacts_address_postal_code')).toBeInTheDocument();
    expect(screen.getByLabelText('lockey_contacts_address_country')).toBeInTheDocument();
    expect(screen.getByLabelText('lockey_contacts_address_is_primary')).toBeInTheDocument();
  });

  it('should render all address type options', () => {
    render(
      <AddressForm onSubmit={vi.fn()} isPending={false} />,
    );

    const typeSelect = screen.getByLabelText('lockey_contacts_address_type');
    expect(typeSelect).toHaveValue('Home');

    expect(screen.getByText('lockey_contacts_address_type_home')).toBeInTheDocument();
    expect(screen.getByText('lockey_contacts_address_type_work')).toBeInTheDocument();
    expect(screen.getByText('lockey_contacts_address_type_billing')).toBeInTheDocument();
    expect(screen.getByText('lockey_contacts_address_type_shipping')).toBeInTheDocument();
  });

  it('should show save label on submit button', () => {
    render(
      <AddressForm onSubmit={vi.fn()} isPending={false} />,
    );

    expect(screen.getByRole('button', { name: 'lockey_common_save' })).toBeInTheDocument();
  });

  it('should show loading label on submit button when isPending', () => {
    render(
      <AddressForm onSubmit={vi.fn()} isPending={true} />,
    );

    const button = screen.getByRole('button', { name: 'lockey_common_loading' });
    expect(button).toBeInTheDocument();
    expect(button).toBeDisabled();
  });

  it('should show validation errors when submitting with empty required fields', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();

    render(
      <AddressForm onSubmit={onSubmit} isPending={false} />,
    );

    await user.click(screen.getByRole('button', { name: 'lockey_common_save' }));

    await waitFor(() => {
      expect(screen.getAllByText('lockey_validation_required').length).toBeGreaterThanOrEqual(3);
    });

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('should populate fields with default values', () => {
    const defaultValues = {
      type: 'Work' as const,
      street1: '123 Main St',
      street2: 'Suite 400',
      city: 'New York',
      state: 'NY',
      postalCode: '10001',
      countryCode: 'US',
      isPrimary: true,
    };

    render(
      <AddressForm onSubmit={vi.fn()} defaultValues={defaultValues} isPending={false} />,
    );

    expect(screen.getByLabelText('lockey_contacts_address_type')).toHaveValue('Work');
    expect(screen.getByLabelText('lockey_contacts_address_street1')).toHaveValue('123 Main St');
    expect(screen.getByLabelText('lockey_contacts_address_street2')).toHaveValue('Suite 400');
    expect(screen.getByLabelText('lockey_contacts_address_city')).toHaveValue('New York');
    expect(screen.getByLabelText('lockey_contacts_address_state')).toHaveValue('NY');
    expect(screen.getByLabelText('lockey_contacts_address_postal_code')).toHaveValue('10001');
    expect(screen.getByLabelText('lockey_contacts_address_country')).toHaveValue('US');
    expect(screen.getByLabelText('lockey_contacts_address_is_primary')).toBeChecked();
  });

  it('should not check isPrimary by default', () => {
    render(
      <AddressForm onSubmit={vi.fn()} isPending={false} />,
    );

    expect(screen.getByLabelText('lockey_contacts_address_is_primary')).not.toBeChecked();
  });

  it('should allow changing the address type', async () => {
    const user = userEvent.setup();

    render(
      <AddressForm onSubmit={vi.fn()} isPending={false} />,
    );

    const typeSelect = screen.getByLabelText('lockey_contacts_address_type');
    await user.selectOptions(typeSelect, 'Billing');

    expect(typeSelect).toHaveValue('Billing');
  });

  it('should allow toggling isPrimary checkbox', async () => {
    const user = userEvent.setup();

    render(
      <AddressForm onSubmit={vi.fn()} isPending={false} />,
    );

    const checkbox = screen.getByLabelText('lockey_contacts_address_is_primary');
    expect(checkbox).not.toBeChecked();

    await user.click(checkbox);

    expect(checkbox).toBeChecked();
  });
});
