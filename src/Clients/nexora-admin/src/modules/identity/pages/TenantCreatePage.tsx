import { useEffect, useMemo } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { useCreateTenant } from '../hooks/useTenants';

function createTenantSchemaFactory(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    name: z.string().min(1, { message: t('lockey_identity_validation_tenant_name_required') }).max(200, { message: t('lockey_identity_validation_tenant_name_max') }),
    slug: z.string().min(1, { message: t('lockey_identity_validation_tenant_slug_required') }).max(63, { message: t('lockey_identity_validation_tenant_slug_max') }).regex(/^[a-z0-9-]+$/, { message: t('lockey_identity_validation_tenant_slug_format') }),
  });
}

export default function TenantCreatePage() {
  const { t } = useTranslation('identity');
  const navigate = useNavigate();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const createTenant = useCreateTenant();
  const { handleApiError } = useApiError();

  const createTenantSchema = useMemo(() => createTenantSchemaFactory(t), [t]);

  const form = useForm({
    resolver: zodResolver(createTenantSchema),
    defaultValues: { name: '', slug: '' },
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_tenants', path: '/identity/tenants' },
      { label: 'lockey_identity_tenants_create' },
    ]);
  }, [setBreadcrumbs]);

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">{t('lockey_identity_tenants_create')}</h1>

      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_identity_tenant_detail_title')}</CardTitle>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={form.handleSubmit((data) => {
              createTenant.mutate(data, {
                onSuccess: () => void navigate('/identity/tenants'),
                onError: (err) => handleApiError(err, form.setError),
              });
            })}
            className="space-y-4"
          >
            <div className="space-y-2">
              <label htmlFor="tenantName" className="text-sm font-medium">
                {t('lockey_identity_form_tenant_name')}
              </label>
              <Input id="tenantName" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-destructive">
                  {form.formState.errors.name.message}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <label htmlFor="tenantSlug" className="text-sm font-medium">
                {t('lockey_identity_form_tenant_slug')}
              </label>
              <Input id="tenantSlug" {...form.register('slug')} />
              <p className="text-xs text-muted-foreground">
                {t('lockey_identity_form_tenant_slug_hint')}
              </p>
              {form.formState.errors.slug && (
                <p className="text-sm text-destructive">
                  {form.formState.errors.slug.message}
                </p>
              )}
            </div>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => navigate(-1)}>
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button type="submit" disabled={createTenant.isPending}>
                {createTenant.isPending
                  ? t('lockey_common_loading', { ns: 'common' })
                  : t('lockey_identity_tenants_create')}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
