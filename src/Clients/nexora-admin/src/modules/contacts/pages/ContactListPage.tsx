import { useCallback, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { SearchInput } from '@/shared/components/data/SearchInput';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useContacts } from '../hooks/useContacts';
import { ContactStatusBadge, ContactTypeBadge } from '../components/ContactStatusBadge';
import type { ContactDto, ContactStatus, ContactType } from '../types';

const VALID_STATUSES: ContactStatus[] = ['Active', 'Archived', 'Merged'];
const VALID_TYPES: ContactType[] = ['Individual', 'Organization'];

export default function ContactListPage() {
  const { t, i18n } = useTranslation('contacts');
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const [searchParams, setSearchParams] = useSearchParams();

  const search = searchParams.get('search') ?? '';

  const rawStatus = searchParams.get('status');
  const statusFilter = VALID_STATUSES.includes(rawStatus as ContactStatus)
    ? (rawStatus as ContactStatus)
    : undefined;

  const rawType = searchParams.get('type');
  const typeFilter = VALID_TYPES.includes(rawType as ContactType)
    ? (rawType as ContactType)
    : undefined;

  const updateFilter = useCallback(
    (key: string, value: string) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        if (value) {
          next.set(key, value);
        } else {
          next.delete(key);
        }
        next.set('page', '1');
        return next;
      });
    },
    [setSearchParams],
  );

  const { data, isPending } = useContacts({
    page,
    pageSize,
    search: search || undefined,
    status: statusFilter,
    type: typeFilter,
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_contacts_module_name', path: '/contacts/contacts' },
    ]);
  }, [setBreadcrumbs]);

  const handleSearchChange = useCallback(
    (value: string) => {
      updateFilter('search', value);
    },
    [updateFilter],
  );

  const handleStatusChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      updateFilter('status', e.target.value);
    },
    [updateFilter],
  );

  const handleTypeChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      updateFilter('type', e.target.value);
    },
    [updateFilter],
  );

  const columns: ColumnDef<ContactDto>[] = [
    {
      key: 'displayName',
      header: t('lockey_contacts_col_display_name'),
      render: (row) => (
        <Link
          to={`/contacts/contacts/${row.id}`}
          className="font-medium text-primary hover:underline"
        >
          {row.displayName}
        </Link>
      ),
    },
    {
      key: 'email',
      header: t('lockey_contacts_col_email'),
      render: (row) => row.email ?? '—',
    },
    {
      key: 'phone',
      header: t('lockey_contacts_col_phone'),
      render: (row) => row.phone ?? '—',
    },
    {
      key: 'type',
      header: t('lockey_contacts_col_type'),
      render: (row) => <ContactTypeBadge type={row.type} />,
    },
    {
      key: 'status',
      header: t('lockey_contacts_col_status'),
      render: (row) => <ContactStatusBadge status={row.status} />,
    },
    {
      key: 'createdAt',
      header: t('lockey_contacts_col_created_at'),
      render: (row) => new Date(row.createdAt).toLocaleDateString(i18n.language),
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_contacts_list_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_contacts_list_description')}
          </p>
        </div>
        <Button type="button" asChild>
          <Link to="/contacts/contacts/create">{t('lockey_contacts_action_create')}</Link>
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <SearchInput
          value={search}
          onChange={handleSearchChange}
          placeholder={t('lockey_contacts_list_search')}
          className="w-72"
        />
        <select
          value={statusFilter ?? ''}
          onChange={handleStatusChange}
          aria-label={t('lockey_contacts_filter_all_statuses')}
          className="rounded-md border border-input bg-background px-3 py-2 text-sm"
        >
          <option value="">{t('lockey_contacts_filter_all_statuses')}</option>
          <option value="Active">{t('lockey_contacts_status_active')}</option>
          <option value="Archived">{t('lockey_contacts_status_archived')}</option>
          <option value="Merged">{t('lockey_contacts_status_merged')}</option>
        </select>
        <select
          value={typeFilter ?? ''}
          onChange={handleTypeChange}
          aria-label={t('lockey_contacts_filter_all_types')}
          className="rounded-md border border-input bg-background px-3 py-2 text-sm"
        >
          <option value="">{t('lockey_contacts_filter_all_types')}</option>
          <option value="Individual">{t('lockey_contacts_type_individual')}</option>
          <option value="Organization">{t('lockey_contacts_type_organization')}</option>
        </select>
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        totalCount={data?.totalCount ?? 0}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        isLoading={isPending}
        emptyMessage={t('lockey_contacts_empty_contacts')}
        keyExtractor={(row) => row.id}
      />
    </div>
  );
}
