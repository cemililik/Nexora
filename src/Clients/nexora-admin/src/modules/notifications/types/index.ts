// Enums as union types
export type NotificationChannel = 'Email' | 'Sms' | 'WhatsApp' | 'Push';
export type NotificationStatus = 'Queued' | 'Sending' | 'Sent' | 'PartialFailure' | 'Failed';
export type RecipientStatus =
  | 'Pending'
  | 'Sent'
  | 'Delivered'
  | 'Opened'
  | 'Clicked'
  | 'Bounced'
  | 'Failed'
  | 'Unsubscribed';
export type ScheduleStatus = 'Pending' | 'Dispatched' | 'Cancelled';
export type TemplateFormat = 'Html' | 'Text' | 'Markdown';
export type ProviderName = 'SendGrid' | 'Mailgun' | 'Twilio' | 'Netgsm' | 'WhatsAppBusiness';

// Notification DTOs
export interface NotificationDto {
  id: string;
  channel: NotificationChannel;
  subject: string;
  status: NotificationStatus;
  triggeredBy: string;
  totalRecipients: number;
  deliveredCount: number;
  failedCount: number;
  queuedAt: string;
  sentAt?: string;
}

export interface NotificationDetailDto {
  id: string;
  templateId?: string;
  channel: NotificationChannel;
  subject: string;
  bodyRendered: string;
  status: NotificationStatus;
  triggeredBy: string;
  triggeredByUserId?: string;
  totalRecipients: number;
  deliveredCount: number;
  failedCount: number;
  openedCount: number;
  clickedCount: number;
  queuedAt: string;
  sentAt?: string;
  recipients: NotificationRecipientDto[];
}

export interface NotificationRecipientDto {
  id: string;
  contactId: string;
  recipientAddress: string;
  status: RecipientStatus;
  providerMessageId?: string;
  failureReason?: string;
  sentAt?: string;
  deliveredAt?: string;
  openedAt?: string;
}

// Template DTOs
export interface NotificationTemplateDto {
  id: string;
  code: string;
  module: string;
  channel: NotificationChannel;
  subject: string;
  format: TemplateFormat;
  isSystem: boolean;
  isActive: boolean;
  createdAt: string;
}

export interface NotificationTemplateDetailDto extends NotificationTemplateDto {
  body: string;
  translations: NotificationTemplateTranslationDto[];
}

export interface NotificationTemplateTranslationDto {
  id: string;
  languageCode: string;
  subject: string;
  body: string;
}

// Provider DTOs
export interface NotificationProviderDto {
  id: string;
  channel: NotificationChannel;
  providerName: ProviderName;
  isDefault: boolean;
  isActive: boolean;
  dailyLimit: number;
  sentToday: number;
  createdAt: string;
}

// Schedule DTOs
export interface NotificationScheduleDto {
  id: string;
  notificationId: string;
  scheduledAt: string;
  status: ScheduleStatus;
  createdAt: string;
}

// Bulk DTOs
export interface BulkNotificationResultDto {
  notificationId: string;
  totalRecipients: number;
  queuedCount: number;
  skippedCount: number;
}

// Request types
export interface SendNotificationRequest {
  channel: NotificationChannel;
  contactId: string;
  recipientAddress: string;
  templateCode?: string;
  subject?: string;
  body?: string;
  variables?: Record<string, string>;
  languageCode?: string;
}

export interface BulkRecipient {
  contactId: string;
  address: string;
}

export interface SendBulkNotificationRequest {
  channel: NotificationChannel;
  recipients: BulkRecipient[];
  templateCode?: string;
  subject?: string;
  body?: string;
  variables?: Record<string, string>;
  languageCode?: string;
}

export interface ScheduleNotificationRequest {
  channel: NotificationChannel;
  contactId: string;
  recipientAddress: string;
  scheduledAt: string;
  templateCode?: string;
  subject?: string;
  body?: string;
  variables?: Record<string, string>;
  languageCode?: string;
}

export interface CreateNotificationTemplateRequest {
  code: string;
  module: string;
  channel: NotificationChannel;
  subject: string;
  body: string;
  format: TemplateFormat;
  isSystem?: boolean;
}

export interface UpdateNotificationTemplateRequest {
  subject: string;
  body: string;
  format: TemplateFormat;
}

export interface AddTemplateTranslationRequest {
  languageCode: string;
  subject: string;
  body: string;
}

export interface CreateNotificationProviderRequest {
  channel: NotificationChannel;
  providerName: ProviderName;
  config: string;
  dailyLimit: number;
  isDefault?: boolean;
}

export interface UpdateNotificationProviderRequest {
  config: string;
  dailyLimit: number;
  isDefault: boolean;
}

export interface TestNotificationProviderRequest {
  testRecipient: string;
}
