import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { ApiEnvelope } from '@/shared/types/api';
import { contactKeys } from './useContacts';

/** Request payload for GDPR erasure (Article 17 right-to-erasure). */
export interface GdprErasureRequest {
  reason: string;
}

/**
 * Hook for issuing a GDPR erasure request against a contact.
 *
 * Calls `POST /contacts/{contactId}/gdpr/delete` and, on success:
 *   - Invalidates both the contacts list query and the detail query for this
 *     contact so any cached data is refreshed.
 *   - Shows a toast using the backend-provided `lockey_` message key (either
 *     `lockey_contacts_gdpr_erasure_enqueued` for async anonymize flow or
 *     `lockey_contacts_gdpr_delete_completed` for synchronous hard-delete).
 *   - Navigates back to the contacts list, since both anonymize and hard-delete
 *     cause the contact to disappear from the default list view.
 */
export function useGdprErasure(contactId: string) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: async (data: GdprErasureRequest): Promise<ApiEnvelope<object>> => {
      return api.postRaw<object>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/gdpr/delete`,
        data,
      );
    },
    onSuccess: (envelope) => {
      void queryClient.invalidateQueries({ queryKey: contactKeys.all });
      void queryClient.invalidateQueries({ queryKey: contactKeys.detail(contactId) });
      const key = envelope.message ?? 'lockey_contacts_gdpr_delete_completed';
      toast.success(t(key, envelope.meta ?? {}));
      void navigate('/contacts/contacts');
    },
    onError: (err) => handleApiError(err),
  });
}
