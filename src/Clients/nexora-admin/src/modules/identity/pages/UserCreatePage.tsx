import { useEffect } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';

import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { useCreateUser } from '../hooks/useUsers';
import { UserForm } from '../components/UserForm';

export default function UserCreatePage() {
  const { t } = useTranslation('identity');
  const navigate = useNavigate();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const createUser = useCreateUser();
  const { handleApiError } = useApiError();

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_users', path: '/identity/users' },
      { label: 'lockey_identity_users_create' },
    ]);
  }, [setBreadcrumbs]);

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">{t('lockey_identity_users_create')}</h1>

      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_identity_tab_profile')}</CardTitle>
        </CardHeader>
        <CardContent>
          <UserForm
            mode="create"
            onSubmit={(data) => {
              createUser.mutate(data, {
                onSuccess: () => void navigate('/identity/users'),
                onError: (err) => handleApiError(err),
              });
            }}
            isPending={createUser.isPending}
          />
        </CardContent>
      </Card>
    </div>
  );
}
