import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { CreateUserRequest, UpdateProfileRequest } from '../types';

function createUserSchemaFactory(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    email: z.string().email({ message: t('lockey_validation_email_invalid', { ns: 'validation' }) }),
    firstName: z.string()
      .min(1, { message: t('lockey_validation_required', { ns: 'validation' }) })
      .max(100, { message: t('lockey_validation_max_length', { ns: 'validation' }) }),
    lastName: z.string()
      .min(1, { message: t('lockey_validation_required', { ns: 'validation' }) })
      .max(100, { message: t('lockey_validation_max_length', { ns: 'validation' }) }),
    temporaryPassword: z.string()
      .min(8, { message: t('lockey_validation_password_min', { ns: 'validation' }) }),
  });
}

function updateProfileSchemaFactory(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    firstName: z.string()
      .min(1, { message: t('lockey_validation_required', { ns: 'validation' }) })
      .max(100, { message: t('lockey_validation_max_length', { ns: 'validation' }) }),
    lastName: z.string()
      .min(1, { message: t('lockey_validation_required', { ns: 'validation' }) })
      .max(100, { message: t('lockey_validation_max_length', { ns: 'validation' }) }),
    phone: z.string().optional(),
  });
}

interface CreateUserFormProps {
  mode: 'create';
  onSubmit: (data: CreateUserRequest) => void;
  isPending: boolean;
}

interface EditUserFormProps {
  mode: 'edit';
  defaultValues: UpdateProfileRequest;
  onSubmit: (data: UpdateProfileRequest) => void;
  isPending: boolean;
}

type UserFormProps = CreateUserFormProps | EditUserFormProps;

export function UserForm(props: UserFormProps) {
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
  onSubmit: (data: CreateUserRequest) => void;
  isPending: boolean;
}) {
  const { t } = useTranslation('identity');
  const form = useForm<CreateUserRequest>({
    resolver: zodResolver(createUserSchemaFactory(t)),
    defaultValues: { email: '', firstName: '', lastName: '', temporaryPassword: '' },
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
      <div className="space-y-2">
        <label htmlFor="email" className="text-sm font-medium">
          {t('lockey_identity_form_email')}
        </label>
        <Input id="email" type="email" {...form.register('email')} />
        {form.formState.errors.email && (
          <p className="text-sm text-destructive">{form.formState.errors.email.message}</p>
        )}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="firstName" className="text-sm font-medium">
            {t('lockey_identity_form_first_name')}
          </label>
          <Input id="firstName" {...form.register('firstName')} />
          {form.formState.errors.firstName && (
            <p className="text-sm text-destructive">{form.formState.errors.firstName.message}</p>
          )}
        </div>
        <div className="space-y-2">
          <label htmlFor="lastName" className="text-sm font-medium">
            {t('lockey_identity_form_last_name')}
          </label>
          <Input id="lastName" {...form.register('lastName')} />
          {form.formState.errors.lastName && (
            <p className="text-sm text-destructive">{form.formState.errors.lastName.message}</p>
          )}
        </div>
      </div>

      <div className="space-y-2">
        <label htmlFor="temporaryPassword" className="text-sm font-medium">
          {t('lockey_identity_form_password')}
        </label>
        <Input id="temporaryPassword" type="password" autoComplete="new-password" {...form.register('temporaryPassword')} />
        {form.formState.errors.temporaryPassword && (
          <p className="text-sm text-destructive">{form.formState.errors.temporaryPassword.message}</p>
        )}
      </div>

      <div className="flex justify-end">
        <Button type="submit" disabled={isPending}>
          {isPending ? t('lockey_common_loading', { ns: 'common' }) : t('lockey_identity_users_create')}
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
  defaultValues: UpdateProfileRequest;
  onSubmit: (data: UpdateProfileRequest) => void;
  isPending: boolean;
}) {
  const { t } = useTranslation('identity');
  const form = useForm<UpdateProfileRequest>({
    resolver: zodResolver(updateProfileSchemaFactory(t)),
    defaultValues,
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="editFirstName" className="text-sm font-medium">
            {t('lockey_identity_form_first_name')}
          </label>
          <Input id="editFirstName" {...form.register('firstName')} />
          {form.formState.errors.firstName && (
            <p className="text-sm text-destructive">{form.formState.errors.firstName.message}</p>
          )}
        </div>
        <div className="space-y-2">
          <label htmlFor="editLastName" className="text-sm font-medium">
            {t('lockey_identity_form_last_name')}
          </label>
          <Input id="editLastName" {...form.register('lastName')} />
          {form.formState.errors.lastName && (
            <p className="text-sm text-destructive">{form.formState.errors.lastName.message}</p>
          )}
        </div>
      </div>

      <div className="space-y-2">
        <label htmlFor="editPhone" className="text-sm font-medium">
          {t('lockey_identity_form_phone')}
        </label>
        <Input id="editPhone" {...form.register('phone')} />
      </div>

      <div className="flex justify-end">
        <Button type="submit" disabled={isPending}>
          {isPending ? t('lockey_common_loading', { ns: 'common' }) : t('lockey_common_save', { ns: 'common' })}
        </Button>
      </div>
    </form>
  );
}
