import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { AddAddressRequest, AddressType } from '../types';

const addressTypes: AddressType[] = ['Home', 'Work', 'Billing', 'Shipping'];

function addressSchemaFactory(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    type: z.enum(['Home', 'Work', 'Billing', 'Shipping'] as const, {
      message: t('lockey_validation_required', { ns: 'validation' }),
    }),
    street1: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
    street2: z.string().optional(),
    city: z.string().min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
    state: z.string().optional(),
    postalCode: z.string().optional(),
    countryCode: z
      .string()
      .min(1, { message: t('lockey_validation_required', { ns: 'validation' }) }),
    isPrimary: z.boolean(),
  });
}

interface AddressFormProps {
  onSubmit: (data: AddAddressRequest) => void;
  defaultValues?: Partial<AddAddressRequest>;
  isPending: boolean;
}

export function AddressForm({ onSubmit, defaultValues, isPending }: AddressFormProps) {
  const { t } = useTranslation('contacts');
  const form = useForm<AddAddressRequest>({
    resolver: zodResolver(addressSchemaFactory(t)),
    defaultValues: {
      type: 'Home',
      street1: '',
      street2: '',
      city: '',
      state: '',
      postalCode: '',
      countryCode: '',
      isPrimary: false,
      ...defaultValues,
    },
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
      <div className="space-y-2">
        <label htmlFor="addressType" className="text-sm font-medium">
          {t('lockey_contacts_address_type')}
        </label>
        <select
          id="addressType"
          className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          {...form.register('type')}
        >
          {addressTypes.map((at) => (
            <option key={at} value={at}>
              {t(`lockey_contacts_address_type_${at.toLowerCase()}`)}
            </option>
          ))}
        </select>
        {form.formState.errors.type && (
          <p className="text-sm text-destructive">{form.formState.errors.type.message}</p>
        )}
      </div>

      <div className="space-y-2">
        <label htmlFor="street1" className="text-sm font-medium">
          {t('lockey_contacts_address_street1')}
        </label>
        <Input id="street1" {...form.register('street1')} />
        {form.formState.errors.street1 && (
          <p className="text-sm text-destructive">{form.formState.errors.street1.message}</p>
        )}
      </div>

      <div className="space-y-2">
        <label htmlFor="street2" className="text-sm font-medium">
          {t('lockey_contacts_address_street2')}
        </label>
        <Input id="street2" {...form.register('street2')} />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="city" className="text-sm font-medium">
            {t('lockey_contacts_address_city')}
          </label>
          <Input id="city" {...form.register('city')} />
          {form.formState.errors.city && (
            <p className="text-sm text-destructive">{form.formState.errors.city.message}</p>
          )}
        </div>
        <div className="space-y-2">
          <label htmlFor="state" className="text-sm font-medium">
            {t('lockey_contacts_address_state')}
          </label>
          <Input id="state" {...form.register('state')} />
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="postalCode" className="text-sm font-medium">
            {t('lockey_contacts_address_postal_code')}
          </label>
          <Input id="postalCode" {...form.register('postalCode')} />
        </div>
        <div className="space-y-2">
          <label htmlFor="countryCode" className="text-sm font-medium">
            {t('lockey_contacts_address_country')}
          </label>
          <Input id="countryCode" {...form.register('countryCode')} />
          {form.formState.errors.countryCode && (
            <p className="text-sm text-destructive">
              {form.formState.errors.countryCode.message}
            </p>
          )}
        </div>
      </div>

      <div className="flex items-center gap-2">
        <input
          id="isPrimary"
          type="checkbox"
          className="h-4 w-4 rounded border-gray-300"
          {...form.register('isPrimary')}
        />
        <label htmlFor="isPrimary" className="text-sm font-medium">
          {t('lockey_contacts_address_is_primary')}
        </label>
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
