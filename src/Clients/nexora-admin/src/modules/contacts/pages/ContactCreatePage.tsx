import { useEffect } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';

import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { useCreateContact } from '../hooks/useContacts';
import { ContactForm } from '../components/ContactForm';

export default function ContactCreatePage() {
  const { t } = useTranslation('contacts');
  const navigate = useNavigate();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const createContact = useCreateContact();
  const { handleApiError } = useApiError();

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_contacts_module_name', path: '/contacts/contacts' },
      { label: 'lockey_contacts_create_title' },
    ]);
  }, [setBreadcrumbs]);

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">{t('lockey_contacts_create_title')}</h1>

      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_contacts_contact_info')}</CardTitle>
        </CardHeader>
        <CardContent>
          <ContactForm
            mode="create"
            onSubmit={(data) => {
              createContact.mutate(data, {
                onSuccess: () => void navigate('/contacts/contacts'),
                onError: (err) => handleApiError(err),
              });
            }}
            isPending={createContact.isPending}
          />
        </CardContent>
      </Card>
    </div>
  );
}
