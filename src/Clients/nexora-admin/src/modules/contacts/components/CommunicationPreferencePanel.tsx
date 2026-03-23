import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import type {
  CommunicationChannel,
  CommunicationPreferenceDto,
  UpdatePreferencesRequest,
} from '../types';

const channels: CommunicationChannel[] = ['Email', 'Sms', 'WhatsApp', 'Phone', 'Mail'];

interface CommunicationPreferencePanelProps {
  preferences: CommunicationPreferenceDto[];
  onUpdate: (data: UpdatePreferencesRequest) => void;
  isPending: boolean;
}

export function CommunicationPreferencePanel({
  preferences,
  onUpdate,
  isPending,
}: CommunicationPreferencePanelProps) {
  const { t } = useTranslation('contacts');

  function buildInitialState(prefs: CommunicationPreferenceDto[]) {
    return Object.fromEntries(
      channels.map((channel) => {
        const pref = prefs.find((p) => p.channel === channel);
        return [channel, pref?.optedIn ?? false];
      }),
    ) as Record<CommunicationChannel, boolean>;
  }

  const [optInState, setOptInState] = useState(() => buildInitialState(preferences));

  useEffect(() => {
    setOptInState(buildInitialState(preferences));
  }, [preferences]);

  function handleToggle(channel: CommunicationChannel) {
    setOptInState((prev) => ({
      ...prev,
      [channel]: !prev[channel],
    }));
  }

  function handleSave() {
    onUpdate({
      preferences: channels.map((channel) => ({
        channel,
        optedIn: optInState[channel],
        optInSource: 'AdminPanel',
      })),
    });
  }

  return (
    <div className="space-y-4">
      <div className="space-y-3">
        {channels.map((channel) => (
          <div
            key={channel}
            className="flex items-center justify-between rounded-md border p-3"
          >
            <span className="text-sm font-medium">
              {t(`lockey_contacts_channel_${channel.toLowerCase()}`)}
            </span>
            <label className="relative inline-flex cursor-pointer items-center">
              <input
                type="checkbox"
                checked={optInState[channel]}
                onChange={() => handleToggle(channel)}
                disabled={isPending}
                className="peer sr-only"
              />
              <div className="peer h-5 w-9 rounded-full bg-gray-200 after:absolute after:start-[2px] after:top-[2px] after:h-4 after:w-4 after:rounded-full after:border after:border-gray-300 after:bg-white after:transition-all after:content-[''] peer-checked:bg-primary peer-checked:after:translate-x-full peer-checked:after:border-white peer-focus:ring-2 peer-focus:ring-ring dark:bg-gray-700" />
              <span className="sr-only">
                {t(`lockey_contacts_channel_${channel.toLowerCase()}`)}
              </span>
            </label>
          </div>
        ))}
      </div>

      <div className="flex justify-end">
        <Button type="button" onClick={handleSave} disabled={isPending}>
          {isPending
            ? t('lockey_common_loading', { ns: 'common' })
            : t('lockey_common_save', { ns: 'common' })}
        </Button>
      </div>
    </div>
  );
}
