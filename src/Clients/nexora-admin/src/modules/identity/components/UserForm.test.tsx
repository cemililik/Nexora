import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { UserForm } from './UserForm';

describe('UserForm', () => {
  describe('CreateForm', () => {
    it('should render email, firstName, lastName, and temporaryPassword fields', () => {
      render(
        <UserForm mode="create" onSubmit={vi.fn()} isPending={false} />,
      );

      expect(screen.getByLabelText('lockey_identity_form_email')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_identity_form_first_name')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_identity_form_last_name')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_identity_form_password')).toBeInTheDocument();
    });

    it('should show create label on submit button', () => {
      render(
        <UserForm mode="create" onSubmit={vi.fn()} isPending={false} />,
      );

      expect(screen.getByRole('button', { name: 'lockey_identity_users_create' })).toBeInTheDocument();
    });

    it('should show loading label on submit button when isPending', () => {
      render(
        <UserForm mode="create" onSubmit={vi.fn()} isPending={true} />,
      );

      const button = screen.getByRole('button', { name: 'lockey_common_loading' });
      expect(button).toBeInTheDocument();
      expect(button).toBeDisabled();
    });

    it('should show validation errors when submitting empty form', async () => {
      const user = userEvent.setup();
      const onSubmit = vi.fn();

      render(
        <UserForm mode="create" onSubmit={onSubmit} isPending={false} />,
      );

      await user.click(screen.getByRole('button', { name: 'lockey_identity_users_create' }));

      await waitFor(() => {
        expect(screen.getByText('lockey_validation_email_invalid')).toBeInTheDocument();
      });

      expect(screen.getAllByText('lockey_validation_required').length).toBeGreaterThanOrEqual(1);
      expect(onSubmit).not.toHaveBeenCalled();
    });
  });

  describe('EditForm', () => {
    const defaultValues = {
      firstName: 'Jane',
      lastName: 'Doe',
      phone: '+1234567890',
    };

    it('should render firstName, lastName, and phone fields', () => {
      render(
        <UserForm
          mode="edit"
          defaultValues={defaultValues}
          onSubmit={vi.fn()}
          isPending={false}
        />,
      );

      expect(screen.getByLabelText('lockey_identity_form_first_name')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_identity_form_last_name')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_identity_form_phone')).toBeInTheDocument();
    });

    it('should not render email or password fields', () => {
      render(
        <UserForm
          mode="edit"
          defaultValues={defaultValues}
          onSubmit={vi.fn()}
          isPending={false}
        />,
      );

      expect(screen.queryByLabelText('lockey_identity_form_email')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('lockey_identity_form_password')).not.toBeInTheDocument();
    });

    it('should show save label on submit button', () => {
      render(
        <UserForm
          mode="edit"
          defaultValues={defaultValues}
          onSubmit={vi.fn()}
          isPending={false}
        />,
      );

      expect(screen.getByRole('button', { name: 'lockey_common_save' })).toBeInTheDocument();
    });

    it('should show loading label on submit button when isPending', () => {
      render(
        <UserForm
          mode="edit"
          defaultValues={defaultValues}
          onSubmit={vi.fn()}
          isPending={true}
        />,
      );

      const button = screen.getByRole('button', { name: 'lockey_common_loading' });
      expect(button).toBeInTheDocument();
      expect(button).toBeDisabled();
    });

    it('should show validation errors when required fields are cleared', async () => {
      const user = userEvent.setup();

      render(
        <UserForm
          mode="edit"
          defaultValues={defaultValues}
          onSubmit={vi.fn()}
          isPending={false}
        />,
      );

      const firstNameInput = screen.getByLabelText('lockey_identity_form_first_name');
      const lastNameInput = screen.getByLabelText('lockey_identity_form_last_name');

      await user.clear(firstNameInput);
      await user.clear(lastNameInput);
      await user.click(screen.getByRole('button', { name: 'lockey_common_save' }));

      await waitFor(() => {
        const errorMessages = screen.getAllByText('lockey_validation_required');
        expect(errorMessages).toHaveLength(2);
      });
    });

    it('should populate fields with default values', () => {
      render(
        <UserForm
          mode="edit"
          defaultValues={defaultValues}
          onSubmit={vi.fn()}
          isPending={false}
        />,
      );

      expect(screen.getByLabelText('lockey_identity_form_first_name')).toHaveValue('Jane');
      expect(screen.getByLabelText('lockey_identity_form_last_name')).toHaveValue('Doe');
      expect(screen.getByLabelText('lockey_identity_form_phone')).toHaveValue('+1234567890');
    });
  });
});
