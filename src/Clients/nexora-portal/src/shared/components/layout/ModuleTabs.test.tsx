import { fireEvent, render, screen } from '@testing-library/react';
import { lazy, type FC } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ModuleTabs } from './ModuleTabs';
import type { PortalModuleManifest } from '@/shared/types/module';

const mockActiveModules: PortalModuleManifest[] = [];
vi.mock('@/shared/hooks/useModules', () => ({
  useModules: () => ({ activeModules: mockActiveModules }),
}));

const mockHasPermission = vi.fn();
vi.mock('@/shared/hooks/usePermissions', () => ({
  usePermissions: () => ({ hasPermission: mockHasPermission }),
}));

// useTranslations returns the key so assertions can match on the raw key.
vi.mock('next-intl', () => ({
  useTranslations: () => (key: string) => key,
}));

vi.mock('@/shared/components/feedback/ErrorBoundary', () => ({
  ErrorBoundary: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

function testComponent(text: string) {
  return lazy(async () => ({
    default: function C() {
      return <div>{text}</div>;
    } as FC,
  }));
}

describe('ModuleTabs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockActiveModules.length = 0;
    mockHasPermission.mockReturnValue(true);
  });

  it('returns null when no builtin or module tabs are available', () => {
    const { container } = render(<ModuleTabs slotId="contact.detail.tabs" />);
    expect(container.firstChild).toBeNull();
  });

  it('renders builtin tabs before module contributions', () => {
    mockActiveModules.push({
      name: 'finance',
      navigation: [],
      permissions: [],
      slots: {
        'contact.detail.tabs': [
          {
            id: 'payments',
            order: 1,
            permissions: [],
            component: testComponent('Payments'),
            labelKey: 'lockey_finance_payments_tab',
          },
        ],
      },
    });

    render(
      <ModuleTabs
        slotId="contact.detail.tabs"
        translationNamespace="common"
        builtinTabs={[
          { id: 'overview', labelKey: 'lockey_contact_overview', render: () => <div>Overview</div> },
        ]}
      />,
    );

    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(2);
    expect(tabs[0]).toHaveTextContent('lockey_contact_overview');
    expect(tabs[1]).toHaveTextContent('lockey_finance_payments_tab');
  });

  it('selects the first tab by default and switches on click', async () => {
    mockActiveModules.push({
      name: 'finance',
      navigation: [],
      permissions: [],
      slots: {
        'contact.detail.tabs': [
          {
            id: 'payments',
            order: 1,
            permissions: [],
            component: testComponent('Payments Panel'),
            labelKey: 'lockey_finance_payments_tab',
          },
        ],
      },
    });

    render(
      <ModuleTabs
        slotId="contact.detail.tabs"
        translationNamespace="common"
        builtinTabs={[
          { id: 'overview', labelKey: 'lockey_contact_overview', render: () => <div>Overview Panel</div> },
        ]}
      />,
    );

    expect(screen.getByText('Overview Panel')).toBeInTheDocument();
    expect(screen.queryByText('Payments Panel')).not.toBeInTheDocument();

    fireEvent.click(screen.getByText('lockey_finance_payments_tab'));

    expect(await screen.findByText('Payments Panel')).toBeInTheDocument();
    expect(screen.queryByText('Overview Panel')).not.toBeInTheDocument();
  });

  it('filters module tabs by permission', () => {
    mockHasPermission.mockImplementation((p: string) => p !== 'finance.read');

    mockActiveModules.push({
      name: 'finance',
      navigation: [],
      permissions: [],
      slots: {
        'contact.detail.tabs': [
          {
            id: 'payments',
            order: 1,
            permissions: ['finance.read'],
            component: testComponent('Payments'),
            labelKey: 'lockey_finance_payments_tab',
          },
        ],
      },
    });

    const { container } = render(
      <ModuleTabs slotId="contact.detail.tabs" translationNamespace="common" />,
    );
    expect(container.firstChild).toBeNull();
  });

  it('ignores contributions without a labelKey', () => {
    mockActiveModules.push({
      name: 'finance',
      navigation: [],
      permissions: [],
      slots: {
        'contact.detail.tabs': [
          {
            id: 'no-label',
            order: 1,
            permissions: [],
            component: testComponent('No label'),
          },
        ],
      },
    });

    const { container } = render(
      <ModuleTabs slotId="contact.detail.tabs" translationNamespace="common" />,
    );
    expect(container.firstChild).toBeNull();
  });
});
