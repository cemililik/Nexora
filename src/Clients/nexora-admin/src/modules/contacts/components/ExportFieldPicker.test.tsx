import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
    i18n: { language: 'en' },
  }),
}));

import { ExportFieldPicker } from './ExportFieldPicker';

const CORE_FIELD_KEYS = [
  'lockey_contacts_export_field_firstname',
  'lockey_contacts_export_field_lastname',
  'lockey_contacts_export_field_email',
  'lockey_contacts_export_field_phone',
  'lockey_contacts_export_field_mobile',
  'lockey_contacts_export_field_website',
  'lockey_contacts_export_field_companyname',
  'lockey_contacts_export_field_taxid',
  'lockey_contacts_export_field_title',
  'lockey_contacts_export_field_type',
  'lockey_contacts_export_field_status',
  'lockey_contacts_export_field_source',
  'lockey_contacts_export_field_language',
  'lockey_contacts_export_field_currency',
  'lockey_contacts_export_field_createdat',
  'lockey_contacts_export_field_updatedat',
];

describe('ExportFieldPicker', () => {
  it('renders a label for each core field', () => {
    render(<ExportFieldPicker selectedFields={[]} onChange={vi.fn()} />);
    for (const key of CORE_FIELD_KEYS) {
      expect(screen.getByText(key)).toBeInTheDocument();
    }
  });

  it('invokes onChange with the updated list when a checkbox toggles', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <ExportFieldPicker selectedFields={['email']} onChange={onChange} />,
    );

    // Toggle firstName on by clicking its label.
    await user.click(
      screen.getByText('lockey_contacts_export_field_firstname'),
    );

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenLastCalledWith(['email', 'firstName']);
  });
});
