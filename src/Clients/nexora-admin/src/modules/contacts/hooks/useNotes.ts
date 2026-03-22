import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import type {
  ContactNoteDto,
  AddNoteRequest,
  UpdateNoteRequest,
  PinNoteRequest,
} from '../types';

export const noteKeys = {
  all: (contactId: string) => ['contacts', 'notes', contactId] as const,
};

export function useNotes(contactId: string) {
  return useQuery({
    queryKey: noteKeys.all(contactId),
    queryFn: () =>
      api.get<ContactNoteDto[]>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/notes`,
      ),
    enabled: !!contactId,
  });
}

export function useAddNote(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');

  return useMutation({
    mutationFn: (data: AddNoteRequest) =>
      api.post<ContactNoteDto>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/notes`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: noteKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_note_added'));
    },
  });
}

export function useUpdateNote(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');

  return useMutation({
    mutationFn: ({ noteId, data }: { noteId: string; data: UpdateNoteRequest }) =>
      api.put<ContactNoteDto>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/notes/${encodeURIComponent(noteId)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: noteKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_note_updated'));
    },
  });
}

export function useDeleteNote(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');

  return useMutation({
    mutationFn: (noteId: string) =>
      api.delete(
        `/contacts/contacts/${encodeURIComponent(contactId)}/notes/${encodeURIComponent(noteId)}`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: noteKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_note_deleted'));
    },
  });
}

export function usePinNote(contactId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ noteId, data }: { noteId: string; data: PinNoteRequest }) =>
      api.put<void>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/notes/${encodeURIComponent(noteId)}/pin`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: noteKeys.all(contactId),
      });
    },
  });
}
