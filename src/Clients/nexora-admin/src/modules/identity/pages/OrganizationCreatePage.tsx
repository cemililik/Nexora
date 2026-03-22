import { useEffect } from 'react';
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
import { useCreateOrganization } from '../hooks/useOrganizations';

const createOrgSchema = z.object({
  name: z.string().min(1).max(200),
  slug: z.string().min(1).max(100).regex(/^[a-z0-9-]+$/),
});

export default function OrganizationCreatePage() {
  const { t } = useTranslation('identity');
  const navigate = useNavigate();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const createOrg = useCreateOrganization();
  const { handleApiError } = useApiError();

  const form = useForm({
    resolver: zodResolver(createOrgSchema),
    defaultValues: { name: '', slug: '' },
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_organizations', path: '/identity/organizations' },
      { label: 'lockey_identity_orgs_create' },
    ]);
  }, [setBreadcrumbs]);

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">{t('lockey_identity_orgs_create')}</h1>

      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_identity_org_detail_title')}</CardTitle>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={form.handleSubmit((data) => {
              createOrg.mutate(data, {
                onSuccess: () => void navigate('/identity/organizations'),
                onError: (err) => handleApiError(err, form.setError),
              });
            })}
            className="space-y-4"
          >
            <div className="space-y-2">
              <label htmlFor="orgName" className="text-sm font-medium">
                {t('lockey_identity_form_org_name')}
              </label>
              <Input id="orgName" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-destructive">
                  {form.formState.errors.name.message}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <label htmlFor="orgSlug" className="text-sm font-medium">
                {t('lockey_identity_form_org_slug')}
              </label>
              <Input id="orgSlug" {...form.register('slug')} />
              {form.formState.errors.slug && (
                <p className="text-sm text-destructive">
                  {form.formState.errors.slug.message}
                </p>
              )}
            </div>
            <div className="flex justify-end gap-2">
              <Button type="submit" disabled={createOrg.isPending}>
                {createOrg.isPending
                  ? t('lockey_common_loading', { ns: 'common' })
                  : t('lockey_identity_orgs_create')}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
