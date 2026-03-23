import type { NotificationChannel, NotificationStatus, ProviderName, RecipientStatus, ScheduleStatus, TemplateFormat } from './types';

export const CHANNELS = ['Email', 'Sms', 'WhatsApp', 'Push'] as const;

export const CHANNEL_KEY_MAP: Record<NotificationChannel, string> = {
  Email: 'lockey_notifications_channel_email',
  Sms: 'lockey_notifications_channel_sms',
  WhatsApp: 'lockey_notifications_channel_whatsapp',
  Push: 'lockey_notifications_channel_push',
};

export const NOTIFICATION_STATUSES = ['Queued', 'Sending', 'Sent', 'PartialFailure', 'Failed'] as const;

export const STATUS_KEY_MAP: Record<NotificationStatus, string> = {
  Queued: 'lockey_notifications_status_queued',
  Sending: 'lockey_notifications_status_sending',
  Sent: 'lockey_notifications_status_sent',
  PartialFailure: 'lockey_notifications_status_partial_failure',
  Failed: 'lockey_notifications_status_failed',
};

export const FORMATS = ['Html', 'Text', 'Markdown'] as const;

export const FORMAT_KEY_MAP: Record<TemplateFormat, string> = {
  Html: 'lockey_notifications_templates_format_html',
  Text: 'lockey_notifications_templates_format_text',
  Markdown: 'lockey_notifications_templates_format_markdown',
};

export const PROVIDER_NAMES = ['SendGrid', 'Mailgun', 'Twilio', 'Netgsm', 'WhatsAppBusiness'] as const;

export const PROVIDER_NAME_KEY_MAP: Record<ProviderName, string> = {
  SendGrid: 'lockey_notifications_providers_name_sendgrid',
  Mailgun: 'lockey_notifications_providers_name_mailgun',
  Twilio: 'lockey_notifications_providers_name_twilio',
  Netgsm: 'lockey_notifications_providers_name_netgsm',
  WhatsAppBusiness: 'lockey_notifications_providers_name_whatsapp_business',
};

export const STATUS_VARIANT_MAP: Record<NotificationStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Queued: 'outline',
  Sending: 'secondary',
  Sent: 'default',
  PartialFailure: 'destructive',
  Failed: 'destructive',
};

export const RECIPIENT_STATUS_KEY_MAP: Record<RecipientStatus, string> = {
  Pending: 'lockey_notifications_recipient_status_pending',
  Sent: 'lockey_notifications_recipient_status_sent',
  Delivered: 'lockey_notifications_recipient_status_delivered',
  Opened: 'lockey_notifications_recipient_status_opened',
  Clicked: 'lockey_notifications_recipient_status_clicked',
  Bounced: 'lockey_notifications_recipient_status_bounced',
  Failed: 'lockey_notifications_recipient_status_failed',
  Unsubscribed: 'lockey_notifications_recipient_status_unsubscribed',
};

export const RECIPIENT_STATUS_VARIANT_MAP: Record<RecipientStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Pending: 'outline',
  Sent: 'secondary',
  Delivered: 'default',
  Opened: 'default',
  Clicked: 'default',
  Bounced: 'destructive',
  Failed: 'destructive',
  Unsubscribed: 'secondary',
};

export const SCHEDULE_STATUS_KEY_MAP: Record<ScheduleStatus, string> = {
  Pending: 'lockey_notifications_schedule_status_pending',
  Dispatched: 'lockey_notifications_schedule_status_dispatched',
  Cancelled: 'lockey_notifications_schedule_status_cancelled',
};

export const SCHEDULE_STATUS_VARIANT_MAP: Record<ScheduleStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Pending: 'outline',
  Dispatched: 'default',
  Cancelled: 'secondary',
};
