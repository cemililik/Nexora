import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { useContacts } from '@/modules/contacts/hooks/useContacts';
import { useLinkContact } from '../hooks/useLinkContact';

export interface LinkContactDialogProps {
  userId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Dialog with a contact search input. Selecting a result + confirming
 * links the user to that contact via {@link useLinkContact}.
 */
export function LinkContactDialog({ userId, open, onOpenChange }: LinkContactDialogProps) {
  const { t } = useTranslation('identity');
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);

  // Debounce search input to avoid firing a query on every keystroke.
  useEffect(() => {
    const id = setTimeout(() => setDebouncedSearch(search), 250);
    return () => clearTimeout(id);
  }, [search]);

  const contactsQuery = useContacts(
    { page: 1, pageSize: 20, search: debouncedSearch.trim() || undefined },
    { enabled: open },
  );
  const linkContact = useLinkContact(userId);

  const items = contactsQuery.data?.items ?? [];

  const reset = () => {
    setSearch('');
    setDebouncedSearch('');
    setSelectedId(null);
  };

  const handleConfirm = () => {
    if (!selectedId) return;
    linkContact.mutate(
      { contactId: selectedId },
      {
        onSuccess: () => {
          reset();
          onOpenChange(false);
        },
      },
    );
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) reset();
        onOpenChange(next);
      }}
    >
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('lockey_identity_user_link_contact_dialog_title')}</DialogTitle>
          <DialogDescription>
            {t('lockey_identity_user_link_contact_dialog_description')}
          </DialogDescription>
        </DialogHeader>
        <Input
          placeholder={t('lockey_identity_user_link_contact_search_placeholder')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label={t('lockey_identity_user_link_contact_search_placeholder')}
        />
        <div
          className="max-h-60 overflow-y-auto space-y-1"
          role="listbox"
          aria-label={t('lockey_identity_user_link_contact_dialog_title')}
        >
          {items.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4 text-center">
              {t('lockey_identity_user_link_contact_empty_results')}
            </p>
          ) : (
            items.map((c) => (
              <button
                key={c.id}
                type="button"
                role="option"
                aria-selected={selectedId === c.id}
                onClick={() => setSelectedId(c.id)}
                className={
                  'flex w-full items-center justify-between rounded-md px-3 py-2 text-sm transition-colors ' +
                  (selectedId === c.id
                    ? 'bg-primary/10 text-primary'
                    : 'hover:bg-accent')
                }
              >
                <span className="truncate">{c.displayName}</span>
                {c.email ? (
                  <span className="text-xs text-muted-foreground truncate ml-2">{c.email}</span>
                ) : null}
              </button>
            ))
          )}
        </div>
        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => {
              reset();
              onOpenChange(false);
            }}
          >
            {t('lockey_identity_user_link_contact_cancel')}
          </Button>
          <Button
            onClick={handleConfirm}
            disabled={!selectedId || linkContact.isPending}
          >
            {t('lockey_identity_user_link_contact_confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default LinkContactDialog;
