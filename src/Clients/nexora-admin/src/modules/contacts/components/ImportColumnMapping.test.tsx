import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ImportColumnMapping, SKIP_MAPPING } from './ImportColumnMapping';

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe('ImportColumnMapping', () => {
  it('renders one row per detected header', () => {
    render(
      <ImportColumnMapping
        headers={['fname', 'lname', 'email_address']}
        mapping={{
          fname: SKIP_MAPPING,
          lname: SKIP_MAPPING,
          email_address: SKIP_MAPPING,
        }}
        onChange={() => {}}
      />,
    );

    expect(screen.getByText('fname')).toBeInTheDocument();
    expect(screen.getByText('lname')).toBeInTheDocument();
    expect(screen.getByText('email_address')).toBeInTheDocument();
  });

  it('exposes a skip option and target field options in the select', async () => {
    const user = userEvent.setup();
    render(
      <ImportColumnMapping
        headers={['col1']}
        mapping={{ col1: 'email' }}
        onChange={() => {}}
      />,
    );

    const trigger = screen.getByRole('combobox');
    await user.click(trigger);

    // After open, both trigger label + listbox item carry the skip/email text —
    // so assert using *AllBy* to tolerate duplicates.
    expect(
      (await screen.findAllByText('lockey_contacts_import_mapping_skip')).length,
    ).toBeGreaterThan(0);
    expect(
      screen.getAllByText('lockey_contacts_import_mapping_field_email').length,
    ).toBeGreaterThan(0);
    expect(
      screen.getByText('lockey_contacts_import_mapping_field_firstname'),
    ).toBeInTheDocument();
  });

  it('fires onChange with the updated mapping when a select value changes', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(
      <ImportColumnMapping
        headers={['col1']}
        mapping={{ col1: SKIP_MAPPING }}
        onChange={onChange}
      />,
    );

    await user.click(screen.getByRole('combobox'));
    await user.click(
      await screen.findByText('lockey_contacts_import_mapping_field_email'),
    );

    expect(onChange).toHaveBeenCalledWith({ col1: 'email' });
  });
});
