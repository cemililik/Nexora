import { useCallback, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { Contact } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { SearchInput } from '@/shared/components/data/SearchInput';
import { EmptyState } from '@/shared/components/feedback/EmptyState';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { formatRelativeTime } from '@/shared/lib/date';
import { useContacts } from '../hooks/useContacts';
import { ContactStatusBadge, ContactTypeBadge } from '../components/ContactStatusBadge';
import type { ContactDto, ContactStatus, ContactType } from '../types';

const VALID_STATUSES: ContactStatus[] = ['Active', 'Archived', 'Merged'];
const VALID_TYPES: ContactType[] = ['Individual', 'Organization'];

export default function ContactListPage() {
  const { t } = useTranslation('contacts');
  const navigate = useNavigate();
  const { page, pageSize, setPage, setPageSize } = usePagination();
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

  const hasActiveFilters = !!(search || statusFilter || typeFilter);

  const emptyStateNode = useMemo(() => {
    if (hasActiveFilters) {
      return (
        <EmptyState
          icon={Contact}
          title={t('lockey_common_no_results_filtered', { ns: 'common' })}
        />
      );
    }
    return (
      <EmptyState
        icon={Contact}
        title={t('lockey_contacts_empty_title')}
        description={t('lockey_contacts_empty_description')}
        action={{ label: t('lockey_contacts_empty_create'), onClick: () => navigate('/contacts/contacts/create') }}
      />
    );
  }, [hasActiveFilters, t, navigate]);

  const handleSearchChange = useCallback(
    (value: string) => {
      updateFilter('search', value);
    },
    [updateFilter],
  );

  const handleStatusChange = useCallback(
    (value: string) => {
      updateFilter('status', value === '__all__' ? '' : value);
    },
    [updateFilter],
  );

  const handleTypeChange = useCallback(
    (value: string) => {
      updateFilter('type', value === '__all__' ? '' : value);
    },
    [updateFilter],
  );

  const columns: ColumnDef<ContactDto>[] = [
    {
      key: 'displayName',
      header: t('lockey_contacts_col_display_name'),
      render: (row) => (
        <span className="font-medium">{row.displayName}</span>
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
      render: (row) => formatRelativeTime(row.createdAt),
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
        <Select value={statusFilter ?? '__all__'} onValueChange={handleStatusChange}>
          <SelectTrigger className="w-48" aria-label={t('lockey_contacts_filter_all_statuses')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_contacts_filter_all_statuses')}</SelectItem>
            <SelectItem value="Active">{t('lockey_contacts_status_active')}</SelectItem>
            <SelectItem value="Archived">{t('lockey_contacts_status_archived')}</SelectItem>
            <SelectItem value="Merged">{t('lockey_contacts_status_merged')}</SelectItem>
          </SelectContent>
        </Select>
        <Select value={typeFilter ?? '__all__'} onValueChange={handleTypeChange}>
          <SelectTrigger className="w-48" aria-label={t('lockey_contacts_filter_all_types')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_contacts_filter_all_types')}</SelectItem>
            <SelectItem value="Individual">{t('lockey_contacts_type_individual')}</SelectItem>
            <SelectItem value="Organization">{t('lockey_contacts_type_organization')}</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        totalCount={data?.totalCount ?? 0}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
        isLoading={isPending}
        emptyState={emptyStateNode}
        keyExtractor={(row) => row.id}
        onRowClick={(row) => navigate(`/contacts/contacts/${row.id}`)}
      />
    </div>
  );
}
