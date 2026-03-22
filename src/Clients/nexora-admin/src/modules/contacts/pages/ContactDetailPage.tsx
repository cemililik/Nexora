import { useEffect, useState } from 'react';
import { useParams } from 'react-router';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { cn } from '@/shared/lib/utils';
import { useApiError } from '@/shared/hooks/useApiError';
import { ContactStatusBadge, ContactTypeBadge } from '../components/ContactStatusBadge';
import {
  useContact,
  useUpdateContact,
  useArchiveContact,
  useRestoreContact,
} from '../hooks/useContacts';
import { useAssignTag, useRemoveTag, useTags } from '../hooks/useTags';
import { useRelationships, useAddRelationship, useRemoveRelationship } from '../hooks/useRelationships';
import { useNotes, useAddNote, useDeleteNote, usePinNote } from '../hooks/useNotes';
import { useActivities } from '../hooks/useActivities';
import { useContactCustomFields, useCustomFieldDefinitions, useSetCustomFieldValue } from '../hooks/useCustomFields';
import { useConsents, useRecordConsent } from '../hooks/useConsents';
import { useCommunicationPreferences, useUpdatePreferences } from '../hooks/useCommunicationPreferences';
import { useDuplicates } from '../hooks/useDuplicates';
import { useGdprExport, useGdprDelete } from '../hooks/useImportExport';
import { ContactForm } from '../components/ContactForm';
import type {
  ContactDetailDto,
  RelationshipType,
  ConsentType,
  CommunicationChannel,
} from '../types';

type TabKey = 'overview' | 'tags' | 'relationships' | 'notes' | 'activities' | 'customFields' | 'gdpr';

export default function ContactDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation('contacts');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();

  const { data: contact, isPending } = useContact(id);
  const updateContact = useUpdateContact(id);
  const archiveContact = useArchiveContact();
  const restoreContact = useRestoreContact();

  const [activeTab, setActiveTab] = useState<TabKey>('overview');
  const [isEditing, setIsEditing] = useState(false);
  const [confirmAction, setConfirmAction] = useState<'archive' | 'restore' | null>(null);
  const [showDuplicates, setShowDuplicates] = useState(false);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_contacts_module_name', path: '/contacts/contacts' },
      { label: contact?.displayName ?? t('lockey_common_loading', { ns: 'common' }) },
    ]);
  }, [setBreadcrumbs, contact]);

  if (isPending) return <LoadingSkeleton lines={8} />;
  if (!contact) return null;

  const tabs: { key: TabKey; label: string }[] = [
    { key: 'overview', label: t('lockey_contacts_tab_overview') },
    { key: 'tags', label: t('lockey_contacts_tab_tags') },
    { key: 'relationships', label: t('lockey_contacts_tab_relationships') },
    { key: 'notes', label: t('lockey_contacts_tab_notes') },
    { key: 'activities', label: t('lockey_contacts_tab_activities') },
    { key: 'customFields', label: t('lockey_contacts_tab_custom_fields') },
    { key: 'gdpr', label: t('lockey_contacts_tab_gdpr') },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{contact.displayName}</h1>
          <div className="flex items-center gap-2 mt-1">
            <ContactTypeBadge type={contact.type} />
            <ContactStatusBadge status={contact.status} />
          </div>
        </div>
        <div className="flex gap-2">
          {contact.status === 'Active' ? (
            <Button
              type="button"
              variant="outline"
              onClick={() => setConfirmAction('archive')}
            >
              {t('lockey_contacts_action_archive')}
            </Button>
          ) : contact.status === 'Archived' ? (
            <Button
              type="button"
              variant="outline"
              onClick={() => setConfirmAction('restore')}
            >
              {t('lockey_contacts_action_restore')}
            </Button>
          ) : null}
          <Button
            type="button"
            variant="outline"
            onClick={() => setShowDuplicates(!showDuplicates)}
          >
            {t('lockey_contacts_action_find_duplicates')}
          </Button>
        </div>
      </div>

      {/* Tab navigation */}
      <div className="flex gap-1 border-b">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => setActiveTab(tab.key)}
            className={cn(
              'px-4 py-2 text-sm font-medium border-b-2 transition-colors',
              activeTab === tab.key
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {activeTab === 'overview' && (
        <OverviewTab
          contact={contact}
          isEditing={isEditing}
          setIsEditing={setIsEditing}
          updateContact={updateContact}
          handleApiError={handleApiError}
          t={t}
          i18n={i18n}
        />
      )}
      {activeTab === 'tags' && <TagsTab contactId={id} t={t} />}
      {activeTab === 'relationships' && <RelationshipsTab contactId={id} t={t} i18n={i18n} />}
      {activeTab === 'notes' && <NotesTab contactId={id} t={t} i18n={i18n} />}
      {activeTab === 'activities' && <ActivitiesTab contactId={id} t={t} i18n={i18n} />}
      {activeTab === 'customFields' && <CustomFieldsTab contactId={id} t={t} />}
      {activeTab === 'gdpr' && (
        <GdprTab
          contactId={id}
          t={t}
          i18n={i18n}
        />
      )}

      {/* Duplicates panel */}
      {showDuplicates && <DuplicatesPanel contactId={id} t={t} />}

      {/* Archive/Restore confirm */}
      <ConfirmDialog
        open={confirmAction === 'archive' || confirmAction === 'restore'}
        onOpenChange={() => setConfirmAction(null)}
        title={
          confirmAction === 'archive'
            ? t('lockey_contacts_action_archive')
            : t('lockey_contacts_action_restore')
        }
        description={
          confirmAction === 'archive'
            ? t('lockey_contacts_confirm_archive')
            : t('lockey_contacts_confirm_restore')
        }
        variant={confirmAction === 'archive' ? 'destructive' : 'default'}
        onConfirm={() => {
          if (confirmAction === 'archive') {
            archiveContact.mutate(id, { onError: (err) => handleApiError(err) });
          } else {
            restoreContact.mutate(id, { onError: (err) => handleApiError(err) });
          }
          setConfirmAction(null);
        }}
        isPending={archiveContact.isPending || restoreContact.isPending}
      />

    </div>
  );
}

/* ─── Overview Tab ─── */

interface OverviewTabProps {
  contact: ContactDetailDto;
  isEditing: boolean;
  setIsEditing: (v: boolean) => void;
  updateContact: ReturnType<typeof useUpdateContact>;
  handleApiError: (err: unknown) => void;
  t: ReturnType<typeof useTranslation>['t'];
  i18n: ReturnType<typeof useTranslation>['i18n'];
}

function OverviewTab({
  contact,
  isEditing,
  setIsEditing,
  updateContact,
  handleApiError,
  t,
  i18n,
}: OverviewTabProps) {
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>{t('lockey_contacts_contact_info')}</CardTitle>
          <Button
            type="button"
            variant={isEditing ? 'outline' : 'default'}
            size="sm"
            onClick={() => setIsEditing(!isEditing)}
          >
            {isEditing
              ? t('lockey_common_cancel', { ns: 'common' })
              : t('lockey_contacts_action_edit')}
          </Button>
        </CardHeader>
        <CardContent>
          {isEditing ? (
            <ContactForm
              mode="edit"
              defaultValues={{
                title: contact.title,
                firstName: contact.firstName,
                lastName: contact.lastName,
                companyName: contact.companyName,
                email: contact.email,
                phone: contact.phone,
                mobile: contact.mobile,
                website: contact.website,
                taxId: contact.taxId,
                language: contact.language,
                currency: contact.currency,
              }}
              onSubmit={(data) => {
                updateContact.mutate(data, {
                  onSuccess: () => setIsEditing(false),
                  onError: (err) => handleApiError(err),
                });
              }}
              isPending={updateContact.isPending}
            />
          ) : (
            <dl className="space-y-3">
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_contacts_col_email')}</dt>
                <dd>{contact.email ?? '—'}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_contacts_col_phone')}</dt>
                <dd>{contact.phone ?? '—'}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_contacts_col_mobile')}</dt>
                <dd>{contact.mobile ?? '—'}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_contacts_col_website')}</dt>
                <dd>{contact.website ?? '—'}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_contacts_col_language')}</dt>
                <dd>{contact.language}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_contacts_col_currency')}</dt>
                <dd>{contact.currency}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_contacts_col_created_at')}</dt>
                <dd>{new Date(contact.createdAt).toLocaleDateString(i18n.language)}</dd>
              </div>
            </dl>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_contacts_addresses_title')}</CardTitle>
        </CardHeader>
        <CardContent>
          {contact.addresses.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {t('lockey_contacts_empty_addresses')}
            </p>
          ) : (
            <ul className="space-y-4">
              {contact.addresses.map((addr) => (
                <li key={addr.id} className="rounded-md border p-3">
                  <div className="flex items-center gap-2 mb-1">
                    <Badge variant="outline">{t(`lockey_contacts_address_type_${addr.type.toLowerCase()}`)}</Badge>
                    {addr.isPrimary && (
                      <Badge variant="secondary">{t('lockey_contacts_address_primary')}</Badge>
                    )}
                  </div>
                  <p className="text-sm">
                    {addr.street1}
                    {addr.street2 ? `, ${addr.street2}` : ''}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {addr.city}
                    {addr.state ? `, ${addr.state}` : ''} {addr.postalCode ?? ''} {addr.countryCode}
                  </p>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

/* ─── Tags Tab ─── */

interface TagsTabProps {
  contactId: string;
  t: ReturnType<typeof useTranslation>['t'];
}

function TagsTab({ contactId, t }: TagsTabProps) {
  const { data: allTags } = useTags();
  const assignTag = useAssignTag();
  const removeTag = useRemoveTag();
  const { data: contact } = useContact(contactId);
  const { handleApiError } = useApiError();

  const assignedTagIds = new Set(contact?.tags.map((ct) => ct.tagId) ?? []);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('lockey_contacts_tab_tags')}</CardTitle>
      </CardHeader>
      <CardContent>
        {contact?.tags.length === 0 && (
          <p className="text-sm text-muted-foreground mb-4">
            {t('lockey_contacts_empty_tags')}
          </p>
        )}

        {/* Assigned tags */}
        <div className="flex flex-wrap gap-2 mb-6">
          {contact?.tags.map((ct) => (
            <Badge key={ct.tagId} variant="default" className="gap-1">
              {ct.color && (
                <span
                  className="inline-block h-2 w-2 rounded-full"
                  // Inline style required: dynamic tag color from data
                  style={{ backgroundColor: ct.color }}
                />
              )}
              {ct.name}
              <button
                type="button"
                className="ms-1 text-xs hover:text-destructive"
                onClick={() => removeTag.mutate({ contactId, tagId: ct.tagId }, { onError: (err) => handleApiError(err) })}
                aria-label={t('lockey_common_remove', { ns: 'common' })}
              >
                &times;
              </button>
            </Badge>
          ))}
        </div>

        {/* Available tags to assign */}
        <div>
          <h4 className="text-sm font-medium mb-2">{t('lockey_contacts_available_tags')}</h4>
          <div className="flex flex-wrap gap-2">
            {allTags
              ?.filter((tag) => tag.isActive && !assignedTagIds.has(tag.id))
              .map((tag) => (
                <Button
                  key={tag.id}
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => assignTag.mutate({ contactId, tagId: tag.id }, { onError: (err) => handleApiError(err) })}
                >
                  {tag.color && (
                    <span
                      className="inline-block h-2 w-2 rounded-full me-1"
                      // Inline style required: dynamic tag color from data
                      style={{ backgroundColor: tag.color }}
                    />
                  )}
                  {tag.name}
                </Button>
              ))}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

/* ─── Relationships Tab ─── */

interface RelationshipsTabProps {
  contactId: string;
  t: ReturnType<typeof useTranslation>['t'];
  i18n: ReturnType<typeof useTranslation>['i18n'];
}

function RelationshipsTab({ contactId, t, i18n }: RelationshipsTabProps) {
  const { data: relationships, isPending } = useRelationships(contactId);
  const addRelationship = useAddRelationship(contactId);
  const removeRelationship = useRemoveRelationship(contactId);
  const { handleApiError } = useApiError();
  const [showForm, setShowForm] = useState(false);
  const [relatedId, setRelatedId] = useState('');
  const [relType, setRelType] = useState<RelationshipType>('ContactOf');

  const relationshipTypes: RelationshipType[] = [
    'ParentOf', 'ChildOf', 'SpouseOf', 'SiblingOf',
    'EmployeeOf', 'EmployerOf', 'ContactOf', 'GuardianOf', 'WardOf',
  ];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>{t('lockey_contacts_tab_relationships')}</CardTitle>
        <Button type="button" size="sm" onClick={() => setShowForm(!showForm)}>
          {showForm
            ? t('lockey_common_cancel', { ns: 'common' })
            : t('lockey_contacts_relationship_add')}
        </Button>
      </CardHeader>
      <CardContent>
        {showForm && (
          <div className="mb-4 flex flex-wrap items-end gap-3 rounded-md border p-3">
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_related_contact_id')}</label>
              <input
                type="text"
                value={relatedId}
                onChange={(e) => setRelatedId(e.target.value)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_relationship_type')}</label>
              <select
                value={relType}
                onChange={(e) => setRelType(e.target.value as RelationshipType)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              >
                {relationshipTypes.map((rt) => (
                  <option key={rt} value={rt}>
                    {t(`lockey_contacts_relationship_type_${rt.replace(/([A-Z])/g, '_$1').toLowerCase().replace(/^_/, '')}`)}
                  </option>
                ))}
              </select>
            </div>
            <Button
              type="button"
              size="sm"
              disabled={!relatedId || addRelationship.isPending}
              onClick={() => {
                addRelationship.mutate(
                  { relatedContactId: relatedId, type: relType },
                  { onSuccess: () => { setRelatedId(''); setShowForm(false); }, onError: (err) => handleApiError(err) },
                );
              }}
            >
              {t('lockey_contacts_relationship_add')}
            </Button>
          </div>
        )}

        {isPending ? (
          <LoadingSkeleton lines={3} />
        ) : relationships?.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            {t('lockey_contacts_empty_relationships')}
          </p>
        ) : (
          <ul className="space-y-2">
            {relationships?.map((rel) => (
              <li key={rel.id} className="flex items-center justify-between rounded-md border p-3">
                <div>
                  <span className="font-medium">{rel.relatedContactDisplayName}</span>
                  <Badge variant="outline" className="ms-2">
                    {t(`lockey_contacts_relationship_type_${rel.type.replace(/([A-Z])/g, '_$1').toLowerCase().replace(/^_/, '')}`)}
                  </Badge>
                  <span className="ms-2 text-xs text-muted-foreground">
                    {new Date(rel.createdAt).toLocaleDateString(i18n.language)}
                  </span>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => removeRelationship.mutate(rel.id, { onError: (err) => handleApiError(err) })}
                  disabled={removeRelationship.isPending}
                >
                  {t('lockey_common_remove', { ns: 'common' })}
                </Button>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

/* ─── Notes Tab ─── */

interface NotesTabProps {
  contactId: string;
  t: ReturnType<typeof useTranslation>['t'];
  i18n: ReturnType<typeof useTranslation>['i18n'];
}

function NotesTab({ contactId, t, i18n }: NotesTabProps) {
  const { data: notes, isPending } = useNotes(contactId);
  const addNote = useAddNote(contactId);
  const deleteNote = useDeleteNote(contactId);
  const pinNote = usePinNote(contactId);
  const { handleApiError } = useApiError();
  const [newContent, setNewContent] = useState('');

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('lockey_contacts_tab_notes')}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="mb-4 flex gap-2">
          <textarea
            value={newContent}
            onChange={(e) => setNewContent(e.target.value)}
            placeholder={t('lockey_contacts_note_placeholder')}
            className="flex-1 rounded-md border border-input bg-background px-3 py-2 text-sm"
            rows={2}
          />
          <Button
            type="button"
            size="sm"
            disabled={!newContent.trim() || addNote.isPending}
            onClick={() => {
              addNote.mutate({ content: newContent.trim() }, {
                onSuccess: () => setNewContent(''),
                onError: (err) => handleApiError(err),
              });
            }}
          >
            {t('lockey_contacts_notes_add')}
          </Button>
        </div>

        {isPending ? (
          <LoadingSkeleton lines={3} />
        ) : notes?.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            {t('lockey_contacts_empty_notes')}
          </p>
        ) : (
          <ul className="space-y-3">
            {notes?.map((note) => (
              <li key={note.id} className="rounded-md border p-3">
                <div className="flex items-center justify-between mb-1">
                  <div className="flex items-center gap-2">
                    {note.isPinned && (
                      <Badge variant="secondary">{t('lockey_contacts_notes_pinned')}</Badge>
                    )}
                    <span className="text-xs text-muted-foreground">
                      {new Date(note.createdAt).toLocaleString(i18n.language)}
                    </span>
                  </div>
                  <div className="flex gap-1">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() =>
                        pinNote.mutate({ noteId: note.id, data: { pin: !note.isPinned } }, { onError: (err) => handleApiError(err) })
                      }
                    >
                      {note.isPinned
                        ? t('lockey_contacts_action_unpin')
                        : t('lockey_contacts_action_pin')}
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => deleteNote.mutate(note.id, { onError: (err) => handleApiError(err) })}
                    >
                      {t('lockey_common_delete', { ns: 'common' })}
                    </Button>
                  </div>
                </div>
                <p className="text-sm whitespace-pre-wrap">{note.content}</p>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

/* ─── Activities Tab ─── */

interface ActivitiesTabProps {
  contactId: string;
  t: ReturnType<typeof useTranslation>['t'];
  i18n: ReturnType<typeof useTranslation>['i18n'];
}

function ActivitiesTab({ contactId, t, i18n }: ActivitiesTabProps) {
  const { data: activities, isPending } = useActivities(contactId);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('lockey_contacts_tab_activities')}</CardTitle>
      </CardHeader>
      <CardContent>
        {isPending ? (
          <LoadingSkeleton lines={4} />
        ) : activities?.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            {t('lockey_contacts_empty_activities')}
          </p>
        ) : (
          <ul className="space-y-3">
            {activities?.map((activity) => (
              <li key={activity.id} className="flex gap-3 border-s-2 border-primary/30 ps-4 py-1">
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <Badge variant="outline">{activity.activityType}</Badge>
                    <Badge variant="secondary">{activity.moduleSource}</Badge>
                  </div>
                  <p className="text-sm mt-1">{activity.summary}</p>
                  {activity.details && (
                    <p className="text-xs text-muted-foreground mt-1">{activity.details}</p>
                  )}
                </div>
                <span className="text-xs text-muted-foreground whitespace-nowrap">
                  {new Date(activity.occurredAt).toLocaleString(i18n.language)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

/* ─── Custom Fields Tab ─── */

interface CustomFieldsTabProps {
  contactId: string;
  t: ReturnType<typeof useTranslation>['t'];
}

function CustomFieldsTab({ contactId, t }: CustomFieldsTabProps) {
  const { data: definitions } = useCustomFieldDefinitions();
  const { data: fieldValues, isPending } = useContactCustomFields(contactId);
  const setFieldValue = useSetCustomFieldValue(contactId);
  const { handleApiError } = useApiError();
  const [editingField, setEditingField] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');

  const valueMap = new Map(
    fieldValues?.map((fv) => [fv.fieldDefinitionId, fv.value]) ?? [],
  );

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('lockey_contacts_tab_custom_fields')}</CardTitle>
      </CardHeader>
      <CardContent>
        {isPending ? (
          <LoadingSkeleton lines={4} />
        ) : definitions?.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            {t('lockey_contacts_empty_custom_fields')}
          </p>
        ) : (
          <dl className="space-y-4">
            {definitions
              ?.filter((d) => d.isActive)
              .sort((a, b) => a.displayOrder - b.displayOrder)
              .map((def) => (
                <div key={def.id} className="flex items-center justify-between rounded-md border p-3">
                  <div>
                    <dt className="text-sm font-medium">
                      {def.fieldName}
                      {def.isRequired && <span className="text-destructive ms-1">*</span>}
                    </dt>
                    {editingField === def.id ? (
                      <div className="flex items-center gap-2 mt-1">
                        {def.fieldType === 'boolean' ? (
                          <select
                            value={editValue}
                            onChange={(e) => setEditValue(e.target.value)}
                            className="rounded-md border border-input bg-background px-3 py-1 text-sm"
                          >
                            <option value="true">{t('lockey_common_yes', { ns: 'common' })}</option>
                            <option value="false">{t('lockey_common_no', { ns: 'common' })}</option>
                          </select>
                        ) : def.fieldType === 'dropdown' && def.options ? (
                          <select
                            value={editValue}
                            onChange={(e) => setEditValue(e.target.value)}
                            className="rounded-md border border-input bg-background px-3 py-1 text-sm"
                          >
                            <option value="">{t('lockey_common_select', { ns: 'common' })}</option>
                            {def.options.split('\n').map((opt) => (
                              <option key={opt} value={opt}>{opt}</option>
                            ))}
                          </select>
                        ) : (
                          <input
                            type={def.fieldType === 'number' ? 'number' : def.fieldType === 'date' ? 'date' : 'text'}
                            value={editValue}
                            onChange={(e) => setEditValue(e.target.value)}
                            className="rounded-md border border-input bg-background px-3 py-1 text-sm"
                          />
                        )}
                        <Button
                          type="button"
                          size="sm"
                          disabled={setFieldValue.isPending}
                          onClick={() => {
                            setFieldValue.mutate(
                              { definitionId: def.id, data: { value: editValue || undefined } },
                              { onSuccess: () => setEditingField(null), onError: (err) => handleApiError(err) },
                            );
                          }}
                        >
                          {t('lockey_common_save', { ns: 'common' })}
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => setEditingField(null)}
                        >
                          {t('lockey_common_cancel', { ns: 'common' })}
                        </Button>
                      </div>
                    ) : (
                      <dd className="text-sm text-muted-foreground mt-1">
                        {valueMap.get(def.id) ?? '—'}
                      </dd>
                    )}
                  </div>
                  {editingField !== def.id && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setEditingField(def.id);
                        setEditValue(valueMap.get(def.id) ?? '');
                      }}
                    >
                      {t('lockey_contacts_action_edit')}
                    </Button>
                  )}
                </div>
              ))}
          </dl>
        )}
      </CardContent>
    </Card>
  );
}

/* ─── GDPR Tab ─── */

interface GdprTabProps {
  contactId: string;
  t: ReturnType<typeof useTranslation>['t'];
  i18n: ReturnType<typeof useTranslation>['i18n'];
}

function GdprTab({ contactId, t, i18n }: GdprTabProps) {
  const { data: consents, isPending: consentsPending } = useConsents(contactId);
  const recordConsent = useRecordConsent(contactId);
  const { data: preferences, isPending: prefsPending } = useCommunicationPreferences(contactId);
  const updatePreferences = useUpdatePreferences(contactId);
  const gdprExport = useGdprExport(contactId);
  const gdprDelete = useGdprDelete(contactId);
  const { handleApiError } = useApiError();

  const [gdprReason, setGdprReason] = useState('');
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  const consentTypes: ConsentType[] = ['EmailMarketing', 'SmsMarketing', 'DataProcessing'];
  const channels: CommunicationChannel[] = ['Email', 'Sms', 'WhatsApp', 'Phone', 'Mail'];

  const consentMap = new Map(
    consents?.map((c) => [c.consentType, c]) ?? [],
  );

  return (
    <div className="space-y-6">
      {/* Consent Panel */}
      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_contacts_gdpr_consents_title')}</CardTitle>
        </CardHeader>
        <CardContent>
          {consentsPending ? (
            <LoadingSkeleton lines={3} />
          ) : (
            <ul className="space-y-3">
              {consentTypes.map((type) => {
                const consent = consentMap.get(type);
                return (
                  <li key={type} className="flex items-center justify-between rounded-md border p-3">
                    <div>
                      <span className="text-sm font-medium">
                        {t(`lockey_contacts_consent_type_${type.replace(/([A-Z])/g, '_$1').toLowerCase().replace(/^_/, '')}`)}
                      </span>
                      {consent && (
                        <span className="ms-2 text-xs text-muted-foreground">
                          {new Date(consent.grantedAt).toLocaleDateString(i18n.language)}
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      <Badge variant={consent?.granted ? 'default' : 'secondary'}>
                        {consent?.granted
                          ? t('lockey_contacts_consent_granted')
                          : t('lockey_contacts_consent_revoked')}
                      </Badge>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={recordConsent.isPending}
                        onClick={() =>
                          recordConsent.mutate({
                            consentType: type,
                            granted: !consent?.granted,
                            source: 'admin',
                          }, { onError: (err) => handleApiError(err) })
                        }
                      >
                        {consent?.granted
                          ? t('lockey_contacts_consent_revoke')
                          : t('lockey_contacts_consent_grant')}
                      </Button>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </CardContent>
      </Card>

      {/* Communication Preferences Panel */}
      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_contacts_gdpr_preferences_title')}</CardTitle>
        </CardHeader>
        <CardContent>
          {prefsPending ? (
            <LoadingSkeleton lines={3} />
          ) : (
            <ul className="space-y-3">
              {channels.map((channel) => {
                const pref = preferences?.find((p) => p.channel === channel);
                return (
                  <li key={channel} className="flex items-center justify-between rounded-md border p-3">
                    <span className="text-sm font-medium">
                      {t(`lockey_contacts_channel_${channel.toLowerCase()}`)}
                    </span>
                    <div className="flex items-center gap-2">
                      <Badge variant={pref?.optedIn ? 'default' : 'secondary'}>
                        {pref?.optedIn
                          ? t('lockey_contacts_preferences_opted_in')
                          : t('lockey_contacts_preferences_opted_out')}
                      </Badge>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={updatePreferences.isPending}
                        onClick={() => {
                          const current = preferences ?? [];
                          const updated = channels.map((ch) => {
                            const existing = current.find((p) => p.channel === ch);
                            return {
                              channel: ch,
                              optedIn: ch === channel ? !pref?.optedIn : (existing?.optedIn ?? false),
                              optInSource: ch === channel ? 'AdminPanel' : (existing?.optInSource ?? 'AdminPanel'),
                            };
                          });
                          updatePreferences.mutate({ preferences: updated }, { onError: (err) => handleApiError(err) });
                        }}
                      >
                        {pref?.optedIn
                          ? t('lockey_contacts_preferences_opt_out')
                          : t('lockey_contacts_preferences_opt_in')}
                      </Button>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </CardContent>
      </Card>

      {/* GDPR Actions */}
      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_contacts_gdpr_actions_title')}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-4">
          <Button
            type="button"
            variant="outline"
            disabled={gdprExport.isPending}
            onClick={() => gdprExport.mutate(undefined, { onError: (err) => handleApiError(err) })}
          >
            {t('lockey_contacts_gdpr_export')}
          </Button>
          <div className="flex items-end gap-2">
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_gdpr_delete_reason')}</label>
              <input
                type="text"
                value={gdprReason}
                onChange={(e) => setGdprReason(e.target.value)}
                className="mt-1 block rounded-md border border-input bg-background px-3 py-2 text-sm"
                placeholder={t('lockey_contacts_gdpr_delete_reason_placeholder')}
              />
            </div>
            <Button
              type="button"
              variant="destructive"
              disabled={!gdprReason.trim() || gdprDelete.isPending}
              onClick={() => setShowDeleteConfirm(true)}
            >
              {t('lockey_contacts_gdpr_delete')}
            </Button>
          </div>
        </CardContent>
      </Card>

      <ConfirmDialog
        open={showDeleteConfirm}
        onOpenChange={() => setShowDeleteConfirm(false)}
        title={t('lockey_contacts_gdpr_delete_title')}
        description={t('lockey_contacts_gdpr_confirm_delete')}
        variant="destructive"
        onConfirm={() => {
          gdprDelete.mutate(
            { reason: gdprReason.trim() },
            {
              onSuccess: () => setShowDeleteConfirm(false),
              onError: (err) => {
                setShowDeleteConfirm(false);
                handleApiError(err);
              },
            },
          );
        }}
        isPending={gdprDelete.isPending}
      />
    </div>
  );
}

/* ─── Duplicates Panel ─── */

interface DuplicatesPanelProps {
  contactId: string;
  t: ReturnType<typeof useTranslation>['t'];
}

function DuplicatesPanel({ contactId, t }: DuplicatesPanelProps) {
  const { data: duplicates, isPending } = useDuplicates(contactId);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('lockey_contacts_duplicates_title')}</CardTitle>
      </CardHeader>
      <CardContent>
        {isPending ? (
          <LoadingSkeleton lines={3} />
        ) : duplicates?.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            {t('lockey_contacts_empty_duplicates')}
          </p>
        ) : (
          <ul className="space-y-2">
            {duplicates?.map((dup) => (
              <li key={dup.contactId} className="flex items-center justify-between rounded-md border p-3">
                <div>
                  <span className="font-medium">{dup.displayName}</span>
                  {dup.email && (
                    <span className="ms-2 text-sm text-muted-foreground">{dup.email}</span>
                  )}
                  <Badge variant="outline" className="ms-2">
                    {t('lockey_contacts_duplicate_score', { score: String(dup.score) })}
                  </Badge>
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
