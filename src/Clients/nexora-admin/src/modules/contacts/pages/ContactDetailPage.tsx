import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useForm, useWatch, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Pencil, Trash2, Search } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { cn } from '@/shared/lib/utils';
import { useApiError } from '@/shared/hooks/useApiError';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { ContactStatusBadge, ContactTypeBadge } from '../components/ContactStatusBadge';
import {
  useContact,
  useContacts,
  useUpdateContact,
  useArchiveContact,
  useRestoreContact,
} from '../hooks/useContacts';
import { useAddresses, useAddAddress, useUpdateAddress, useDeleteAddress } from '../hooks/useAddresses';
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
  ContactAddressDto,
  AddressType,
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

  const { hasPermission } = usePermissions();
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
  }, [setBreadcrumbs, contact, t]);

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
          {hasPermission('contacts.contact.update') && (
            contact.status === 'Active' ? (
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
            ) : null
          )}
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
              contactType={contact.type}
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

      <AddressesCard contactId={contact.id} addresses={contact.addresses} t={t} />
    </div>
  );
}

/* ─── Addresses Card ─── */

const addressTypes: AddressType[] = ['Home', 'Work', 'Billing', 'Shipping'];

const createAddressSchema = (t: (key: string, options?: Record<string, unknown>) => string) =>
  z.object({
    type: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
    street1: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
    street2: z.string().optional(),
    city: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
    state: z.string().optional(),
    postalCode: z.string().optional(),
    countryCode: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
    isPrimary: z.boolean().optional(),
  });

type AddressFormValues = z.infer<ReturnType<typeof createAddressSchema>>;

interface AddressesCardProps {
  contactId: string;
  addresses: ContactAddressDto[];
  t: ReturnType<typeof useTranslation>['t'];
}

function AddressesCard({ contactId, addresses: initialAddresses, t }: AddressesCardProps) {
  const { data: fetchedAddresses } = useAddresses(contactId);
  const addAddress = useAddAddress(contactId);
  const updateAddress = useUpdateAddress(contactId);
  const deleteAddress = useDeleteAddress(contactId);
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();
  const canCreate = hasPermission('contacts.contact.update');
  const canUpdate = hasPermission('contacts.contact.update');
  const canDelete = hasPermission('contacts.contact.update');

  const addresses = fetchedAddresses ?? initialAddresses;

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingAddress, setEditingAddress] = useState<ContactAddressDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ContactAddressDto | null>(null);

  const addressSchema = useMemo(() => createAddressSchema(t), [t]);
  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors },
  } = useForm<AddressFormValues>({
    resolver: zodResolver(addressSchema),
    defaultValues: {
      type: 'Home',
      street1: '',
      street2: '',
      city: '',
      state: '',
      postalCode: '',
      countryCode: '',
      isPrimary: false,
    },
  });

  const openAddDialog = () => {
    setEditingAddress(null);
    reset({
      type: 'Home',
      street1: '',
      street2: '',
      city: '',
      state: '',
      postalCode: '',
      countryCode: '',
      isPrimary: false,
    });
    setDialogOpen(true);
  };

  const openEditDialog = (addr: ContactAddressDto) => {
    setEditingAddress(addr);
    reset({
      type: addr.type,
      street1: addr.street1,
      street2: addr.street2 ?? '',
      city: addr.city,
      state: addr.state ?? '',
      postalCode: addr.postalCode ?? '',
      countryCode: addr.countryCode,
      isPrimary: addr.isPrimary,
    });
    setDialogOpen(true);
  };

  const onSubmit = (data: AddressFormValues) => {
    const payload = {
      type: data.type as AddressType,
      street1: data.street1,
      street2: data.street2 || undefined,
      city: data.city,
      state: data.state || undefined,
      postalCode: data.postalCode || undefined,
      countryCode: data.countryCode,
    };

    if (editingAddress) {
      updateAddress.mutate(
        { addressId: editingAddress.id, data: payload },
        {
          onSuccess: () => { setDialogOpen(false); reset(); },
          onError: (err) => handleApiError(err),
        },
      );
    } else {
      addAddress.mutate(
        { ...payload, isPrimary: data.isPrimary ?? false },
        {
          onSuccess: () => { setDialogOpen(false); reset(); },
          onError: (err) => handleApiError(err),
        },
      );
    }
  };

  return (
    <>
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>{t('lockey_contacts_addresses_title')}</CardTitle>
          {canCreate && (
            <Button type="button" size="sm" onClick={openAddDialog}>
              {t('lockey_contacts_address_add')}
            </Button>
          )}
        </CardHeader>
        <CardContent>
          {addresses.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {t('lockey_contacts_empty_addresses')}
            </p>
          ) : (
            <ul className="space-y-4">
              {addresses.map((addr) => (
                <li key={addr.id} className="rounded-md border p-3">
                  <div className="flex items-center justify-between mb-1">
                    <div className="flex items-center gap-2">
                      <Badge variant="outline">{t(`lockey_contacts_address_type_${addr.type.toLowerCase()}`)}</Badge>
                      {addr.isPrimary && (
                        <Badge variant="secondary">{t('lockey_contacts_address_primary')}</Badge>
                      )}
                    </div>
                    <div className="flex gap-1">
                      {canUpdate && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          title={t('lockey_contacts_address_edit')}
                          onClick={() => openEditDialog(addr)}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                      )}
                      {canDelete && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          title={t('lockey_contacts_address_delete')}
                          onClick={() => setDeleteTarget(addr)}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      )}
                    </div>
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

      {/* Add/Edit Address Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>
              {editingAddress
                ? t('lockey_contacts_address_edit')
                : t('lockey_contacts_address_add')}
            </DialogTitle>
            <DialogDescription className="sr-only">
              {editingAddress
                ? t('lockey_contacts_address_edit')
                : t('lockey_contacts_address_add')}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_address_form_type')}</label>
              <Controller
                name="type"
                control={control}
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger className="mt-1">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {addressTypes.map((at) => (
                        <SelectItem key={at} value={at}>
                          {t(`lockey_contacts_address_type_${at.toLowerCase()}`)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.type && (
                <p className="text-xs text-destructive mt-1">{errors.type.message}</p>
              )}
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_address_form_street1')}</label>
              <Input {...register('street1')} className="mt-1" />
              {errors.street1 && (
                <p className="text-xs text-destructive mt-1">{errors.street1.message}</p>
              )}
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_address_form_street2')}</label>
              <Input {...register('street2')} className="mt-1" />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="text-sm font-medium">{t('lockey_contacts_address_form_city')}</label>
                <Input {...register('city')} className="mt-1" />
                {errors.city && (
                  <p className="text-xs text-destructive mt-1">{errors.city.message}</p>
                )}
              </div>
              <div>
                <label className="text-sm font-medium">{t('lockey_contacts_address_form_state')}</label>
                <Input {...register('state')} className="mt-1" />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="text-sm font-medium">{t('lockey_contacts_address_form_postal_code')}</label>
                <Input {...register('postalCode')} className="mt-1" />
              </div>
              <div>
                <label className="text-sm font-medium">{t('lockey_contacts_address_form_country_code')}</label>
                <Input {...register('countryCode')} className="mt-1" />
                {errors.countryCode && (
                  <p className="text-xs text-destructive mt-1">{errors.countryCode.message}</p>
                )}
              </div>
            </div>
            {!editingAddress && (
              <div className="flex items-center gap-2">
                <input
                  type="checkbox"
                  id="isPrimary"
                  {...register('isPrimary')}
                  className="rounded border-input"
                />
                <label htmlFor="isPrimary" className="text-sm font-medium">
                  {t('lockey_contacts_address_form_is_primary')}
                </label>
              </div>
            )}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button
                type="submit"
                disabled={addAddress.isPending || updateAddress.isPending}
              >
                {editingAddress
                  ? t('lockey_common_save', { ns: 'common' })
                  : t('lockey_contacts_address_add')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Delete Address Confirmation */}
      <ConfirmDialog
        open={deleteTarget !== null}
        onOpenChange={() => setDeleteTarget(null)}
        title={t('lockey_contacts_address_delete')}
        description={t('lockey_contacts_address_delete_confirm')}
        variant="destructive"
        onConfirm={() => {
          if (deleteTarget) {
            deleteAddress.mutate(deleteTarget.id, {
              onSuccess: () => setDeleteTarget(null),
              onError: (err) => {
                handleApiError(err);
                setDeleteTarget(null);
              },
            });
          }
        }}
        isPending={deleteAddress.isPending}
      />
    </>
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
  const { hasPermission } = usePermissions();
  const canManageTags = hasPermission('contacts.tag.update');

  const assignedTagIds = useMemo(
    () => new Set(contact?.tags.map((ct) => ct.tagId) ?? []),
    [contact?.tags],
  );

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
              {canManageTags && (
                <button
                  type="button"
                  className="ms-1 text-xs hover:text-destructive"
                  onClick={() => removeTag.mutate({ contactId, tagId: ct.tagId }, { onError: (err) => handleApiError(err) })}
                  aria-label={t('lockey_common_remove', { ns: 'common' })}
                >
                  &times;
                </button>
              )}
            </Badge>
          ))}
        </div>

        {/* Available tags to assign */}
        {canManageTags && (
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
        )}
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

const createRelationshipSchema = (t: (key: string, options?: Record<string, unknown>) => string) =>
  z.object({
    relatedContactId: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
    type: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
  });

type RelationshipFormValues = z.infer<ReturnType<typeof createRelationshipSchema>>;

function RelationshipsTab({ contactId, t, i18n }: RelationshipsTabProps) {
  const { data: relationships, isPending } = useRelationships(contactId);
  const addRelationship = useAddRelationship(contactId);
  const removeRelationship = useRemoveRelationship(contactId);
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();
  const canCreate = hasPermission('contacts.relationship.create');
  const canDelete = hasPermission('contacts.relationship.delete');
  const [showForm, setShowForm] = useState(false);
  const [contactSearch, setContactSearch] = useState('');
  const [selectedContact, setSelectedContact] = useState<{ id: string; displayName: string } | null>(null);
  const [showContactSearch, setShowContactSearch] = useState(false);

  const { data: searchResults } = useContacts({
    page: 1,
    pageSize: 20,
    search: contactSearch || undefined,
  });

  const relationshipSchema = useMemo(() => createRelationshipSchema(t), [t]);
  const {
    register,
    handleSubmit,
    reset,
    setValue,
    formState: { errors },
  } = useForm<RelationshipFormValues>({
    resolver: zodResolver(relationshipSchema),
    defaultValues: { relatedContactId: '', type: 'ContactOf' },
  });

  const relationshipTypes: RelationshipType[] = [
    'ParentOf', 'ChildOf', 'SpouseOf', 'SiblingOf',
    'EmployeeOf', 'EmployerOf', 'ContactOf', 'GuardianOf', 'WardOf',
  ];

  const filteredContacts = useMemo(() => {
    const items = searchResults?.items ?? [];
    return items.filter((c) => c.id !== contactId);
  }, [searchResults, contactId]);

  const handleSelectContact = (contact: { id: string; displayName: string }) => {
    setSelectedContact(contact);
    setValue('relatedContactId', contact.id);
    setShowContactSearch(false);
    setContactSearch('');
  };

  const onSubmit = (data: RelationshipFormValues) => {
    addRelationship.mutate(
      { relatedContactId: data.relatedContactId, type: data.type as RelationshipType },
      {
        onSuccess: () => {
          reset();
          setSelectedContact(null);
          setShowForm(false);
        },
        onError: (err) => handleApiError(err),
      },
    );
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>{t('lockey_contacts_tab_relationships')}</CardTitle>
        {canCreate && (
          <Button type="button" size="sm" onClick={() => { setShowForm(!showForm); setSelectedContact(null); setContactSearch(''); }}>
            {showForm
              ? t('lockey_common_cancel', { ns: 'common' })
              : t('lockey_contacts_relationship_add')}
          </Button>
        )}
      </CardHeader>
      <CardContent>
        {showForm && canCreate && (
          <form onSubmit={handleSubmit(onSubmit)} className="mb-4 flex flex-wrap items-end gap-3 rounded-md border p-3">
            <input type="hidden" {...register('relatedContactId')} />
            <div className="w-full sm:w-auto sm:min-w-[240px]">
              <label className="text-sm font-medium">{t('lockey_contacts_relationship_related_contact')}</label>
              <div className="relative mt-1">
                {selectedContact ? (
                  <div className="flex items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm">
                    <span>{selectedContact.displayName}</span>
                    <button
                      type="button"
                      className="ms-2 text-muted-foreground hover:text-foreground"
                      onClick={() => {
                        setSelectedContact(null);
                        setValue('relatedContactId', '');
                        setShowContactSearch(true);
                      }}
                    >
                      &times;
                    </button>
                  </div>
                ) : (
                  <>
                    <div className="relative">
                      <Search className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                      <Input
                        placeholder={t('lockey_contacts_relationship_search_contacts')}
                        value={contactSearch}
                        onChange={(e) => {
                          setContactSearch(e.target.value);
                          setShowContactSearch(true);
                        }}
                        onFocus={() => setShowContactSearch(true)}
                        className="ps-9"
                      />
                    </div>
                    {showContactSearch && (
                      <div className="absolute z-10 mt-1 w-full max-h-48 overflow-y-auto rounded-md border bg-popover shadow-md">
                        {filteredContacts.length === 0 ? (
                          <p className="px-3 py-4 text-sm text-muted-foreground text-center">
                            {t('lockey_contacts_relationship_no_contacts_found')}
                          </p>
                        ) : (
                          filteredContacts.map((c) => (
                            <button
                              key={c.id}
                              type="button"
                              className="flex w-full items-center px-3 py-2 text-sm hover:bg-accent transition-colors text-start"
                              onClick={() => handleSelectContact({ id: c.id, displayName: c.displayName })}
                            >
                              <div>
                                <p className="font-medium">{c.displayName}</p>
                                {c.email && (
                                  <p className="text-xs text-muted-foreground">{c.email}</p>
                                )}
                              </div>
                            </button>
                          ))
                        )}
                      </div>
                    )}
                  </>
                )}
              </div>
              {errors.relatedContactId && (
                <p className="text-xs text-destructive mt-1">{errors.relatedContactId.message}</p>
              )}
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_relationship_type')}</label>
              <select
                {...register('type')}
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
              type="submit"
              size="sm"
              disabled={addRelationship.isPending || !selectedContact}
            >
              {t('lockey_contacts_relationship_add')}
            </Button>
          </form>
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
                {canDelete && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => removeRelationship.mutate(rel.id, { onError: (err) => handleApiError(err) })}
                    disabled={removeRelationship.isPending}
                  >
                    {t('lockey_common_remove', { ns: 'common' })}
                  </Button>
                )}
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

const createNoteSchema = (t: (key: string, options?: Record<string, unknown>) => string) =>
  z.object({
    content: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
  });

type NoteFormValues = z.infer<ReturnType<typeof createNoteSchema>>;

function NotesTab({ contactId, t, i18n }: NotesTabProps) {
  const { data: notes, isPending } = useNotes(contactId);
  const addNote = useAddNote(contactId);
  const deleteNote = useDeleteNote(contactId);
  const pinNote = usePinNote(contactId);
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();
  const canCreate = hasPermission('contacts.note.create');
  const canUpdate = hasPermission('contacts.note.update');
  const canDelete = hasPermission('contacts.note.delete');

  const noteSchema = useMemo(() => createNoteSchema(t), [t]);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isValid },
  } = useForm<NoteFormValues>({
    resolver: zodResolver(noteSchema),
    defaultValues: { content: '' },
    mode: 'onChange',
  });

  const onSubmit = (data: NoteFormValues) => {
    addNote.mutate({ content: data.content.trim() }, {
      onSuccess: () => reset(),
      onError: (err) => handleApiError(err),
    });
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('lockey_contacts_tab_notes')}</CardTitle>
      </CardHeader>
      <CardContent>
        {canCreate && (
          <form onSubmit={handleSubmit(onSubmit)} className="mb-4 flex gap-2">
            <div className="flex-1">
              <textarea
                {...register('content')}
                placeholder={t('lockey_contacts_note_placeholder')}
                className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                rows={2}
              />
              {errors.content && (
                <p className="text-xs text-destructive mt-1">{errors.content.message}</p>
              )}
            </div>
            <Button
              type="submit"
              size="sm"
              disabled={!isValid || addNote.isPending}
            >
              {t('lockey_contacts_notes_add')}
            </Button>
          </form>
        )}

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
                    {canUpdate && (
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
                    )}
                    {canDelete && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() => deleteNote.mutate(note.id, { onError: (err) => handleApiError(err) })}
                      >
                        {t('lockey_common_delete', { ns: 'common' })}
                      </Button>
                    )}
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

const createCustomFieldSchema = () =>
  z.object({
    value: z.string().optional(),
  });

type CustomFieldFormValues = z.infer<ReturnType<typeof createCustomFieldSchema>>;

function CustomFieldsTab({ contactId, t }: CustomFieldsTabProps) {
  const { data: definitions } = useCustomFieldDefinitions();
  const { data: fieldValues, isPending } = useContactCustomFields(contactId);
  const setFieldValue = useSetCustomFieldValue(contactId);
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('contacts.custom-field.manage');
  const [editingField, setEditingField] = useState<string | null>(null);

  const customFieldSchema = useMemo(() => createCustomFieldSchema(), []);
  const {
    register,
    handleSubmit,
    reset,
  } = useForm<CustomFieldFormValues>({
    resolver: zodResolver(customFieldSchema),
    defaultValues: { value: '' },
  });

  const valueMap = useMemo(
    () => new Map(fieldValues?.map((fv) => [fv.fieldDefinitionId, fv.value]) ?? []),
    [fieldValues],
  );

  const startEditing = (defId: string) => {
    setEditingField(defId);
    reset({ value: valueMap.get(defId) ?? '' });
  };

  const onSubmit = (defId: string) => (data: CustomFieldFormValues) => {
    setFieldValue.mutate(
      { definitionId: defId, data: { value: data.value || undefined } },
      {
        onSuccess: () => { setEditingField(null); reset(); },
        onError: (err) => handleApiError(err),
      },
    );
  };

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
                      <form onSubmit={handleSubmit(onSubmit(def.id))} className="flex items-center gap-2 mt-1">
                        {def.fieldType === 'boolean' ? (
                          <select
                            {...register('value')}
                            className="rounded-md border border-input bg-background px-3 py-1 text-sm"
                          >
                            <option value="true">{t('lockey_common_yes', { ns: 'common' })}</option>
                            <option value="false">{t('lockey_common_no', { ns: 'common' })}</option>
                          </select>
                        ) : def.fieldType === 'dropdown' && def.options ? (
                          <select
                            {...register('value')}
                            className="rounded-md border border-input bg-background px-3 py-1 text-sm"
                          >
                            <option value="">{t('lockey_common_select', { ns: 'common' })}</option>
                            {def.options.split(/\r?\n/).map(o => o.trim()).filter(Boolean).map((opt) => (
                              <option key={opt} value={opt}>{opt}</option>
                            ))}
                          </select>
                        ) : (
                          <input
                            type={def.fieldType === 'number' ? 'number' : def.fieldType === 'date' ? 'date' : 'text'}
                            {...register('value')}
                            className="rounded-md border border-input bg-background px-3 py-1 text-sm"
                          />
                        )}
                        <Button
                          type="submit"
                          size="sm"
                          disabled={setFieldValue.isPending}
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
                      </form>
                    ) : (
                      <dd className="text-sm text-muted-foreground mt-1">
                        {valueMap.get(def.id) ?? '—'}
                      </dd>
                    )}
                  </div>
                  {editingField !== def.id && canManage && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => startEditing(def.id)}
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

const createGdprDeleteSchema = (t: (key: string, options?: Record<string, unknown>) => string) =>
  z.object({
    reason: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
  });

type GdprDeleteFormValues = z.infer<ReturnType<typeof createGdprDeleteSchema>>;

function GdprTab({ contactId, t, i18n }: GdprTabProps) {
  const { data: consents, isPending: consentsPending } = useConsents(contactId);
  const recordConsent = useRecordConsent(contactId);
  const { data: preferences, isPending: prefsPending } = useCommunicationPreferences(contactId);
  const updatePreferences = useUpdatePreferences(contactId);
  const gdprExport = useGdprExport(contactId);
  const gdprDelete = useGdprDelete(contactId);
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission('contacts.gdpr.export');
  const canDelete = hasPermission('contacts.gdpr.delete');

  const gdprDeleteSchema = useMemo(() => createGdprDeleteSchema(t), [t]);
  const {
    register,
    reset,
    control,
    formState: { errors },
  } = useForm<GdprDeleteFormValues>({
    resolver: zodResolver(gdprDeleteSchema),
    defaultValues: { reason: '' },
  });

  const reasonValue = useWatch({ control, name: 'reason' });
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  const consentTypes: ConsentType[] = ['EmailMarketing', 'SmsMarketing', 'DataProcessing'];
  const channels: CommunicationChannel[] = ['Email', 'Sms', 'WhatsApp', 'Phone', 'Mail'];

  const consentMap = useMemo(
    () => new Map(consents?.map((c) => [c.consentType, c]) ?? []),
    [consents],
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
      {(canExport || canDelete) && (
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_contacts_gdpr_actions_title')}</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-4">
            {canExport && (
              <Button
                type="button"
                variant="outline"
                disabled={gdprExport.isPending}
                onClick={() => gdprExport.mutate(undefined, { onError: (err) => handleApiError(err) })}
              >
                {t('lockey_contacts_gdpr_export')}
              </Button>
            )}
            {canDelete && (
              <div className="flex items-end gap-2">
                <div>
                  <label className="text-sm font-medium">{t('lockey_contacts_gdpr_delete_reason')}</label>
                  <input
                    type="text"
                    {...register('reason')}
                    className="mt-1 block rounded-md border border-input bg-background px-3 py-2 text-sm"
                    placeholder={t('lockey_contacts_gdpr_delete_reason_placeholder')}
                  />
                  {errors.reason && (
                    <p className="text-xs text-destructive mt-1">{errors.reason.message}</p>
                  )}
                </div>
                <Button
                  type="button"
                  variant="destructive"
                  disabled={!reasonValue.trim() || gdprDelete.isPending}
                  onClick={() => setShowDeleteConfirm(true)}
                >
                  {t('lockey_contacts_gdpr_delete')}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      <ConfirmDialog
        open={showDeleteConfirm}
        onOpenChange={() => setShowDeleteConfirm(false)}
        title={t('lockey_contacts_gdpr_delete_title')}
        description={t('lockey_contacts_gdpr_confirm_delete')}
        variant="destructive"
        onConfirm={() => {
          gdprDelete.mutate(
            { reason: reasonValue.trim() },
            {
              onSuccess: () => { setShowDeleteConfirm(false); reset(); },
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
