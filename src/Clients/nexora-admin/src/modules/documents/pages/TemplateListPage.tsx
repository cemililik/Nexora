import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { useApiError } from '@/shared/hooks/useApiError';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { useTemplates, useActivateTemplate, useDeactivateTemplate } from '../hooks/useTemplates';
import type { DocumentTemplateDto, TemplateCategory } from '../types';

const CATEGORIES: TemplateCategory[] = ['Contract', 'Receipt', 'Letter', 'Report'];

export default function TemplateListPage() {
  const { t, i18n } = useTranslation('documents');
  const navigate = useNavigate();
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('documents.template.manage');
  const { handleApiError } = useApiError();

  const [searchParams, setSearchParams] = useSearchParams();
  const rawCategory = searchParams.get('category');
  const category = CATEGORIES.includes(rawCategory as TemplateCategory)
    ? (rawCategory as TemplateCategory)
    : undefined;

  const activateTemplate = useActivateTemplate();
  const deactivateTemplate = useDeactivateTemplate();

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_documents_module_name' },
      { label: 'lockey_documents_templates_title' },
    ]);
  }, [setBreadcrumbs]);

  const { data, isPending } = useTemplates({
    page,
    pageSize,
    category,
  });

  const columns: ColumnDef<DocumentTemplateDto>[] = [
    {
      key: 'name',
      header: t('lockey_documents_templates_col_name'),
      render: (row) => row.name,
    },
    {
      key: 'category',
      header: t('lockey_documents_templates_col_category'),
      render: (row) =>
        t(`lockey_documents_templates_category_${row.category.toLowerCase()}`),
    },
    {
      key: 'format',
      header: t('lockey_documents_templates_col_format'),
      render: (row) =>
        t(`lockey_documents_templates_format_${row.format.toLowerCase()}`),
    },
    {
      key: 'isActive',
      header: t('lockey_documents_templates_col_active'),
      render: (row) => (
        <Badge variant={row.isActive ? 'default' : 'secondary'}>
          {row.isActive
            ? t('lockey_documents_status_active')
            : t('lockey_documents_status_archived')}
        </Badge>
      ),
    },
    {
      key: 'createdAt',
      header: t('lockey_documents_templates_col_created_at'),
      render: (row) => new Date(row.createdAt).toLocaleDateString(i18n.language),
    },
    {
      key: 'actions',
      header: t('lockey_documents_templates_col_actions'),
      render: (row) => (
        <div className="flex gap-1">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/documents/templates/${row.id}`)}
          >
            {t('lockey_documents_action_edit')}
          </Button>
          {canManage && (
            <Button
              type="button"
              variant="ghost"
              size="sm"
              disabled={activateTemplate.isPending || deactivateTemplate.isPending}
              onClick={() => {
                if (row.isActive) {
                  deactivateTemplate.mutate(row.id, { onError: (err) => handleApiError(err) });
                } else {
                  activateTemplate.mutate(row.id, { onError: (err) => handleApiError(err) });
                }
              }}
            >
              {row.isActive
                ? t('lockey_documents_templates_deactivate')
                : t('lockey_documents_templates_activate')}
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_documents_templates_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_documents_templates_description')}
          </p>
        </div>
        {canManage && (
          <Button type="button" onClick={() => navigate('/documents/templates/create')}>
            {t('lockey_documents_templates_create')}
          </Button>
        )}
      </div>

      <div className="flex items-center gap-4">
        <Select
          value={category ?? '__all__'}
          onValueChange={(v) => {
            setSearchParams((prev: URLSearchParams) => {
              const next = new URLSearchParams(prev);
              if (v === '__all__') {
                next.delete('category');
              } else {
                next.set('category', v);
              }
              next.set('page', '1');
              return next;
            });
          }}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_documents_templates_filter_category')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_documents_templates_filter_all_categories')}</SelectItem>
            {CATEGORIES.map((c) => (
              <SelectItem key={c} value={c}>
                {t(`lockey_documents_templates_category_${c.toLowerCase()}`)}
              </SelectItem>
            ))}
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
        isLoading={isPending}
        emptyMessage={t('lockey_documents_templates_empty')}
        keyExtractor={(row) => row.id}
      />
    </div>
  );
}
