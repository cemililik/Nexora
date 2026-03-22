import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pin, PinOff, Pencil, Trash2 } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import type { ContactNoteDto } from '../types';

interface NoteListProps {
  notes: ContactNoteDto[];
  onAdd: (content: string) => void;
  onUpdate: (noteId: string, content: string) => void;
  onDelete: (noteId: string) => void;
  onPin: (noteId: string, pin: boolean) => void;
  isPending: boolean;
}

export function NoteList({ notes, onAdd, onUpdate, onDelete, onPin, isPending }: NoteListProps) {
  const { t, i18n } = useTranslation('contacts');
  const [newContent, setNewContent] = useState('');
  const [editingNoteId, setEditingNoteId] = useState<string | null>(null);
  const [editContent, setEditContent] = useState('');
  const [deleteNoteId, setDeleteNoteId] = useState<string | null>(null);

  const sortedNotes = [...notes].sort((a, b) => {
    if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1;
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
  });

  function handleAdd() {
    const trimmed = newContent.trim();
    if (trimmed) {
      onAdd(trimmed);
      setNewContent('');
    }
  }

  function handleStartEdit(note: ContactNoteDto) {
    setEditingNoteId(note.id);
    setEditContent(note.content);
  }

  function handleSaveEdit() {
    if (editingNoteId && editContent.trim()) {
      onUpdate(editingNoteId, editContent.trim());
      setEditingNoteId(null);
      setEditContent('');
    }
  }

  function handleCancelEdit() {
    setEditingNoteId(null);
    setEditContent('');
  }

  function handleConfirmDelete() {
    if (deleteNoteId) {
      onDelete(deleteNoteId);
      setDeleteNoteId(null);
    }
  }

  return (
    <div className="space-y-4">
      <div className="space-y-2">
        <textarea
          value={newContent}
          onChange={(e) => setNewContent(e.target.value)}
          placeholder={t('lockey_contacts_notes_placeholder')}
          className="flex min-h-[80px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          rows={3}
        />
        <div className="flex justify-end">
          <Button
            type="button"
            size="sm"
            onClick={handleAdd}
            disabled={isPending || !newContent.trim()}
          >
            {t('lockey_contacts_notes_add')}
          </Button>
        </div>
      </div>

      {sortedNotes.length === 0 && (
        <p className="text-sm text-muted-foreground">{t('lockey_contacts_empty_notes')}</p>
      )}

      <div className="space-y-3">
        {sortedNotes.map((note) => (
          <div
            key={note.id}
            className="rounded-md border p-3 space-y-2"
          >
            {editingNoteId === note.id ? (
              <div className="space-y-2">
                <textarea
                  value={editContent}
                  onChange={(e) => setEditContent(e.target.value)}
                  className="flex min-h-[60px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                  rows={3}
                />
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="outline" size="sm" onClick={handleCancelEdit}>
                    {t('lockey_common_cancel', { ns: 'common' })}
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    onClick={handleSaveEdit}
                    disabled={isPending}
                  >
                    {t('lockey_common_save', { ns: 'common' })}
                  </Button>
                </div>
              </div>
            ) : (
              <>
                <p className="text-sm whitespace-pre-wrap">{note.content}</p>
                <div className="flex items-center justify-between">
                  <span className="text-xs text-muted-foreground">
                    {new Date(note.createdAt).toLocaleString(i18n.language)}
                    {note.updatedAt && (
                      <> &middot; {t('lockey_contacts_notes_edited')}</>
                    )}
                  </span>
                  <div className="flex items-center gap-1">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => onPin(note.id, !note.isPinned)}
                      disabled={isPending}
                      aria-label={
                        note.isPinned
                          ? t('lockey_contacts_action_unpin')
                          : t('lockey_contacts_action_pin')
                      }
                    >
                      {note.isPinned ? (
                        <PinOff className="h-4 w-4" />
                      ) : (
                        <Pin className="h-4 w-4" />
                      )}
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => handleStartEdit(note)}
                      disabled={isPending}
                      aria-label={t('lockey_contacts_action_edit')}
                    >
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => setDeleteNoteId(note.id)}
                      disabled={isPending}
                      aria-label={t('lockey_contacts_notes_delete')}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </>
            )}
          </div>
        ))}
      </div>

      <ConfirmDialog
        open={deleteNoteId !== null}
        onOpenChange={(open) => {
          if (!open) setDeleteNoteId(null);
        }}
        title={t('lockey_contacts_notes_delete_title')}
        description={t('lockey_contacts_notes_delete_description')}
        onConfirm={handleConfirmDelete}
        variant="destructive"
        isPending={isPending}
      />
    </div>
  );
}
