import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import type { ContactStatus, ContactType } from '../types';

const contactStatusStyles: Record<ContactStatus, string> = {
  Active: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
  Archived: 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200',
  Merged: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200',
};

const contactStatusKeys: Record<ContactStatus, string> = {
  Active: 'lockey_contacts_status_active',
  Archived: 'lockey_contacts_status_archived',
  Merged: 'lockey_contacts_status_merged',
};

export function ContactStatusBadge({ status }: { status: ContactStatus }) {
  const { t } = useTranslation('contacts');
  return (
    <Badge variant="outline" className={cn(contactStatusStyles[status])}>
      {t(contactStatusKeys[status])}
    </Badge>
  );
}

const contactTypeStyles: Record<ContactType, string> = {
  Individual: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
  Organization: 'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200',
};

const contactTypeKeys: Record<ContactType, string> = {
  Individual: 'lockey_contacts_type_individual',
  Organization: 'lockey_contacts_type_organization',
};

export function ContactTypeBadge({ type }: { type: ContactType }) {
  const { t } = useTranslation('contacts');
  return (
    <Badge variant="outline" className={cn(contactTypeStyles[type])}>
      {t(contactTypeKeys[type])}
    </Badge>
  );
}
