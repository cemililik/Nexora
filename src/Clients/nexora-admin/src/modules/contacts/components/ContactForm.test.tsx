import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ContactForm } from './ContactForm';

describe('ContactForm', () => {
  describe('CreateForm', () => {
    it('should render type, source, email, and phone fields', () => {
      render(
        <ContactForm mode="create" onSubmit={vi.fn()} isPending={false} />,
      );

      expect(screen.getByLabelText('lockey_contacts_form_type')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_source')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_email')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_phone')).toBeInTheDocument();
    });

    it('should render Individual fields by default', () => {
      render(
        <ContactForm mode="create" onSubmit={vi.fn()} isPending={false} />,
      );

      expect(screen.getByLabelText('lockey_contacts_form_title')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_first_name')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_last_name')).toBeInTheDocument();
      expect(screen.queryByLabelText('lockey_contacts_form_company_name')).not.toBeInTheDocument();
    });

    it('should show companyName field when type is Organization', async () => {
      const user = userEvent.setup();
      render(
        <ContactForm mode="create" onSubmit={vi.fn()} isPending={false} />,
      );

      // shadcn Select uses Radix combobox — click trigger then option
      const trigger = screen.getByLabelText('lockey_contacts_form_type');
      await user.click(trigger);
      const option = await screen.findByRole('option', { name: 'lockey_contacts_type_organization' });
      await user.click(option);

      await waitFor(() => {
        expect(screen.getByLabelText('lockey_contacts_form_company_name')).toBeInTheDocument();
      });

      expect(screen.queryByLabelText('lockey_contacts_form_first_name')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('lockey_contacts_form_last_name')).not.toBeInTheDocument();
    });

    it('should show create label on submit button', () => {
      render(
        <ContactForm mode="create" onSubmit={vi.fn()} isPending={false} />,
      );

      expect(screen.getByRole('button', { name: 'lockey_contacts_create' })).toBeInTheDocument();
    });

    it('should show loading label on submit button when isPending', () => {
      render(
        <ContactForm mode="create" onSubmit={vi.fn()} isPending={true} />,
      );

      const button = screen.getByRole('button', { name: 'lockey_common_loading' });
      expect(button).toBeInTheDocument();
      expect(button).toBeDisabled();
    });

    it('should show validation errors when submitting Individual with empty names', async () => {
      const user = userEvent.setup();
      const onSubmit = vi.fn();

      render(
        <ContactForm mode="create" onSubmit={onSubmit} isPending={false} />,
      );

      await user.click(screen.getByRole('button', { name: 'lockey_contacts_create' }));

      await waitFor(() => {
        expect(screen.getAllByText('lockey_validation_required').length).toBeGreaterThanOrEqual(2);
      });

      expect(onSubmit).not.toHaveBeenCalled();
    });
  });

  describe('EditForm', () => {
    const defaultValues = {
      title: 'Mr.',
      firstName: 'John',
      lastName: 'Smith',
      companyName: 'Acme Inc.',
      email: 'john@example.com',
      phone: '+1234567890',
      mobile: '+0987654321',
      website: 'https://example.com',
      taxId: 'TX123',
      language: 'en',
      currency: 'USD',
    };

    it('should render all edit mode fields', () => {
      render(
        <ContactForm
          mode="edit"
          contactType="Individual"
          defaultValues={defaultValues}
          onSubmit={vi.fn()}
          isPending={false}
        />,
      );

      expect(screen.getByLabelText('lockey_contacts_form_title')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_first_name')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_last_name')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_company_name')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_email')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_phone')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_mobile')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_website')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_tax_id')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_language')).toBeInTheDocument();
      expect(screen.getByLabelText('lockey_contacts_form_currency')).toBeInTheDocument();
    });

    it('should populate fields with default values', () => {
      render(
        <ContactForm
          mode="edit"
          contactType="Individual"
          defaultValues={defaultValues}
          onSubmit={vi.fn()}
          isPending={false}
        />,
      );

      expect(screen.getByLabelText('lockey_contacts_form_title')).toHaveValue('Mr.');
      expect(screen.getByLabelText('lockey_contacts_form_first_name')).toHaveValue('John');
      expect(screen.getByLabelText('lockey_contacts_form_last_name')).toHaveValue('Smith');
      expect(screen.getByLabelText('lockey_contacts_form_email')).toHaveValue('john@example.com');
      // shadcn Select renders as combobox — check displayed text instead of value
      expect(screen.getByLabelText('lockey_contacts_form_language')).toHaveTextContent('lockey_contacts_language_en');
      expect(screen.getByLabelText('lockey_contacts_form_currency')).toHaveTextContent('lockey_contacts_currency_usd');
    });

    it('should show save label on submit button', () => {
      render(
        <ContactForm
          mode="edit"
          contactType="Individual"
          defaultValues={defaultValues}
          onSubmit={vi.fn()}
          isPending={false}
        />,
      );

      expect(screen.getByRole('button', { name: 'lockey_common_save' })).toBeInTheDocument();
    });

    it('should show loading label on submit button when isPending', () => {
      render(
        <ContactForm
          mode="edit"
          contactType="Individual"
          defaultValues={defaultValues}
          onSubmit={vi.fn()}
          isPending={true}
        />,
      );

      const button = screen.getByRole('button', { name: 'lockey_common_loading' });
      expect(button).toBeInTheDocument();
      expect(button).toBeDisabled();
    });
  });
});
