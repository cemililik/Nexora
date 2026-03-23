import { useMemo } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { toSnakeCase } from '@/shared/lib/utils';
import type {
  CreateContactRequest,
  UpdateContactRequest,
  ContactType,
  ContactSource,
} from '../types';

function createContactSchemaFactory(t: (key: string, options?: Record<string, unknown>) => string) {
  return z
    .object({
      type: z.enum(['Individual', 'Organization'] as const, {
        message: t('lockey_validation_required', { ns: 'validation' }),
      }),
      title: z.string().optional(),
      firstName: z.string().optional(),
      lastName: z.string().optional(),
      companyName: z.string().optional(),
      email: z
        .string()
        .email({ message: t('lockey_validation_email_invalid', { ns: 'validation' }) })
        .optional()
        .or(z.literal('')),
      phone: z.string().optional(),
      source: z.enum(['WebForm', 'Import', 'Manual', 'Api'] as const, {
        message: t('lockey_validation_required', { ns: 'validation' }),
      }),
    })
    .superRefine((data, ctx) => {
      if (data.type === 'Individual') {
        if (!data.firstName || data.firstName.trim().length === 0) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ['firstName'],
            message: t('lockey_validation_required', { ns: 'validation' }),
          });
        }
        if (!data.lastName || data.lastName.trim().length === 0) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ['lastName'],
            message: t('lockey_validation_required', { ns: 'validation' }),
          });
        }
      }
      if (data.type === 'Organization') {
        if (!data.companyName || data.companyName.trim().length === 0) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ['companyName'],
            message: t('lockey_validation_required', { ns: 'validation' }),
          });
        }
      }
    });
}

function updateContactSchemaFactory(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    title: z.string().optional(),
    firstName: z.string().optional(),
    lastName: z.string().optional(),
    companyName: z.string().optional(),
    email: z
      .string()
      .email({ message: t('lockey_validation_email_invalid', { ns: 'validation' }) })
      .optional()
      .or(z.literal('')),
    phone: z.string().optional(),
    mobile: z.string().optional(),
    website: z.string().optional(),
    taxId: z.string().optional(),
    language: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
    currency: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
  });
}

const contactTypes: ContactType[] = ['Individual', 'Organization'];
const contactSources: ContactSource[] = ['WebForm', 'Import', 'Manual', 'Api'];

interface CreateContactFormProps {
  mode: 'create';
  onSubmit: (data: CreateContactRequest) => void;
  isPending: boolean;
}

interface EditContactFormProps {
  mode: 'edit';
  defaultValues: UpdateContactRequest;
  onSubmit: (data: UpdateContactRequest) => void;
  isPending: boolean;
}

type ContactFormProps = CreateContactFormProps | EditContactFormProps;

export function ContactForm(props: ContactFormProps) {
  if (props.mode === 'create') {
    return <CreateForm onSubmit={props.onSubmit} isPending={props.isPending} />;
  }

  return (
    <EditForm
      defaultValues={props.defaultValues}
      onSubmit={props.onSubmit}
      isPending={props.isPending}
    />
  );
}

function CreateForm({
  onSubmit,
  isPending,
}: {
  onSubmit: (data: CreateContactRequest) => void;
  isPending: boolean;
}) {
  const { t } = useTranslation('contacts');
  const schema = useMemo(() => createContactSchemaFactory(t), [t]);
  const form = useForm<CreateContactRequest>({
    resolver: zodResolver(schema),
    defaultValues: {
      type: 'Individual',
      title: '',
      firstName: '',
      lastName: '',
      companyName: '',
      email: '',
      phone: '',
      source: 'Manual',
    },
  });

  const selectedType = useWatch({ control: form.control, name: 'type' });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="type" className="text-sm font-medium">
            {t('lockey_contacts_form_type')}
          </label>
          <Controller
            control={form.control}
            name="type"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {contactTypes.map((ct) => (
                    <SelectItem key={ct} value={ct}>
                      {t(`lockey_contacts_type_${ct.toLowerCase()}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          {form.formState.errors.type && (
            <p className="text-sm text-destructive">{form.formState.errors.type.message}</p>
          )}
        </div>

        <div className="space-y-2">
          <label htmlFor="source" className="text-sm font-medium">
            {t('lockey_contacts_form_source')}
          </label>
          <Controller
            control={form.control}
            name="source"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="source">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {contactSources.map((cs) => (
                    <SelectItem key={cs} value={cs}>
                      {t(`lockey_contacts_source_${toSnakeCase(cs)}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          {form.formState.errors.source && (
            <p className="text-sm text-destructive">{form.formState.errors.source.message}</p>
          )}
        </div>
      </div>

      {selectedType === 'Individual' && (
        <>
          <div className="space-y-2">
            <label htmlFor="title" className="text-sm font-medium">
              {t('lockey_contacts_form_title')}
            </label>
            <Input id="title" {...form.register('title')} />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <label htmlFor="firstName" className="text-sm font-medium">
                {t('lockey_contacts_form_first_name')}
              </label>
              <Input id="firstName" {...form.register('firstName')} />
              {form.formState.errors.firstName && (
                <p className="text-sm text-destructive">
                  {form.formState.errors.firstName.message}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <label htmlFor="lastName" className="text-sm font-medium">
                {t('lockey_contacts_form_last_name')}
              </label>
              <Input id="lastName" {...form.register('lastName')} />
              {form.formState.errors.lastName && (
                <p className="text-sm text-destructive">
                  {form.formState.errors.lastName.message}
                </p>
              )}
            </div>
          </div>
        </>
      )}

      {selectedType === 'Organization' && (
        <div className="space-y-2">
          <label htmlFor="companyName" className="text-sm font-medium">
            {t('lockey_contacts_form_company_name')}
          </label>
          <Input id="companyName" {...form.register('companyName')} />
          {form.formState.errors.companyName && (
            <p className="text-sm text-destructive">
              {form.formState.errors.companyName.message}
            </p>
          )}
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="email" className="text-sm font-medium">
            {t('lockey_contacts_form_email')}
          </label>
          <Input id="email" type="email" {...form.register('email')} />
          {form.formState.errors.email && (
            <p className="text-sm text-destructive">{form.formState.errors.email.message}</p>
          )}
        </div>
        <div className="space-y-2">
          <label htmlFor="phone" className="text-sm font-medium">
            {t('lockey_contacts_form_phone')}
          </label>
          <Input id="phone" {...form.register('phone')} />
        </div>
      </div>

      <div className="flex justify-end">
        <Button type="submit" disabled={isPending}>
          {isPending
            ? t('lockey_common_loading', { ns: 'common' })
            : t('lockey_contacts_create')}
        </Button>
      </div>
    </form>
  );
}

function EditForm({
  defaultValues,
  onSubmit,
  isPending,
}: {
  defaultValues: UpdateContactRequest;
  onSubmit: (data: UpdateContactRequest) => void;
  isPending: boolean;
}) {
  const { t } = useTranslation('contacts');
  const schema = useMemo(() => updateContactSchemaFactory(t), [t]);
  const form = useForm<UpdateContactRequest>({
    resolver: zodResolver(schema),
    defaultValues,
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
      <div className="space-y-2">
        <label htmlFor="editTitle" className="text-sm font-medium">
          {t('lockey_contacts_form_title')}
        </label>
        <Input id="editTitle" {...form.register('title')} />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="editFirstName" className="text-sm font-medium">
            {t('lockey_contacts_form_first_name')}
          </label>
          <Input id="editFirstName" {...form.register('firstName')} />
        </div>
        <div className="space-y-2">
          <label htmlFor="editLastName" className="text-sm font-medium">
            {t('lockey_contacts_form_last_name')}
          </label>
          <Input id="editLastName" {...form.register('lastName')} />
        </div>
      </div>

      <div className="space-y-2">
        <label htmlFor="editCompanyName" className="text-sm font-medium">
          {t('lockey_contacts_form_company_name')}
        </label>
        <Input id="editCompanyName" {...form.register('companyName')} />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="editEmail" className="text-sm font-medium">
            {t('lockey_contacts_form_email')}
          </label>
          <Input id="editEmail" type="email" {...form.register('email')} />
          {form.formState.errors.email && (
            <p className="text-sm text-destructive">{form.formState.errors.email.message}</p>
          )}
        </div>
        <div className="space-y-2">
          <label htmlFor="editPhone" className="text-sm font-medium">
            {t('lockey_contacts_form_phone')}
          </label>
          <Input id="editPhone" {...form.register('phone')} />
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="editMobile" className="text-sm font-medium">
            {t('lockey_contacts_form_mobile')}
          </label>
          <Input id="editMobile" {...form.register('mobile')} />
        </div>
        <div className="space-y-2">
          <label htmlFor="editWebsite" className="text-sm font-medium">
            {t('lockey_contacts_form_website')}
          </label>
          <Input id="editWebsite" {...form.register('website')} />
        </div>
      </div>

      <div className="space-y-2">
        <label htmlFor="editTaxId" className="text-sm font-medium">
          {t('lockey_contacts_form_tax_id')}
        </label>
        <Input id="editTaxId" {...form.register('taxId')} />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="editLanguage" className="text-sm font-medium">
            {t('lockey_contacts_form_language')}
          </label>
          <Controller
            control={form.control}
            name="language"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="editLanguage">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="en">{t('lockey_contacts_language_en')}</SelectItem>
                  <SelectItem value="tr">{t('lockey_contacts_language_tr')}</SelectItem>
                  <SelectItem value="ar">{t('lockey_contacts_language_ar')}</SelectItem>
                </SelectContent>
              </Select>
            )}
          />
          {form.formState.errors.language && (
            <p className="text-sm text-destructive">{form.formState.errors.language.message}</p>
          )}
        </div>
        <div className="space-y-2">
          <label htmlFor="editCurrency" className="text-sm font-medium">
            {t('lockey_contacts_form_currency')}
          </label>
          <Controller
            control={form.control}
            name="currency"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="editCurrency">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="USD">{t('lockey_contacts_currency_usd')}</SelectItem>
                  <SelectItem value="EUR">{t('lockey_contacts_currency_eur')}</SelectItem>
                  <SelectItem value="TRY">{t('lockey_contacts_currency_try')}</SelectItem>
                </SelectContent>
              </Select>
            )}
          />
          {form.formState.errors.currency && (
            <p className="text-sm text-destructive">{form.formState.errors.currency.message}</p>
          )}
        </div>
      </div>

      <div className="flex justify-end">
        <Button type="submit" disabled={isPending}>
          {isPending
            ? t('lockey_common_loading', { ns: 'common' })
            : t('lockey_common_save', { ns: 'common' })}
        </Button>
      </div>
    </form>
  );
}
