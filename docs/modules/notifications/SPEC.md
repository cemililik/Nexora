# Module: Notification Engine

## Overview
The Notification Engine is a **core platform module** that provides unified communication infrastructure for all other modules. It handles email, SMS, WhatsApp, and push notification delivery with template management, delivery tracking, bulk sending with throttling, and per-contact communication preferences/consent enforcement. No module sends notifications directly — they all publish events, and this module handles the delivery.

## Domain Model

### Entities

```mermaid
---
title: Notification Engine - Entity Relationship Diagram
---
erDiagram
    NotificationTemplate ||--o{ NotificationTemplateTranslation : "translated to"
    NotificationTemplate {
        uuid id PK
        uuid organization_id FK "nullable (system templates)"
        string code UK "donation_confirmed, lead_assigned, ..."
        string module "donations, crm, education, ..."
        string channel "email, sms, whatsapp, push"
        string subject "for email"
        string body "with variable placeholders"
        string format "html, text, markdown"
        boolean is_system "non-editable"
        boolean is_active
    }

    NotificationTemplateTranslation {
        uuid id PK
        uuid template_id FK
        string language_code "en, tr, ar, ..."
        string subject
        string body
    }

    Notification ||--o{ NotificationRecipient : "sent to"
    Notification {
        uuid id PK
        uuid organization_id FK
        uuid template_id FK "nullable (custom content)"
        string channel "email, sms, whatsapp, push"
        string subject
        string body_rendered "final content after variable substitution"
        string status "queued, sending, sent, partial_failure, failed"
        string triggered_by "event name or user action"
        uuid triggered_by_user_id FK "nullable"
        int total_recipients
        int delivered_count
        int failed_count
        int opened_count
        int clicked_count
        timestamp queued_at
        timestamp sent_at
    }

    NotificationRecipient {
        uuid id PK
        uuid notification_id FK
        uuid contact_id FK
        string recipient_address "email or phone"
        string status "pending, sent, delivered, opened, clicked, bounced, failed, unsubscribed"
        string failure_reason
        string provider_message_id
        timestamp sent_at
        timestamp delivered_at
        timestamp opened_at
    }

    NotificationProvider {
        uuid id PK
        uuid tenant_id FK
        string channel "email, sms, whatsapp"
        string provider_name "sendgrid, mailgun, twilio, netgsm, whatsapp_business"
        jsonb config "api_key (encrypted), from_address, sender_id, ..."
        boolean is_default
        boolean is_active
        int daily_limit
        int sent_today
    }

    NotificationSchedule {
        uuid id PK
        uuid notification_id FK
        datetime scheduled_at
        string status "pending, dispatched, cancelled"
    }
```

### Domain Events

| Event | Trigger | Consumers |
|-------|---------|-----------|
| `NotificationSent` | All recipients processed | Audit log |
| `NotificationDelivered` | Provider confirms delivery | Analytics |
| `NotificationOpened` | Recipient opens email | Campaign analytics (CRM) |
| `NotificationBounced` | Email bounced | Contacts (flag invalid email) |
| `NotificationFailed` | Delivery failed | Admin alert, retry queue |

### Delivery Flow

```mermaid
---
title: Notification Delivery Pipeline
---
flowchart TB
    Event["Module Event\n(e.g., DonationConfirmed)"] --> Handler["Notification Handler\n(resolve template, recipients)"]
    Handler --> Consent["Check Consent\n(KVKK/GDPR)"]
    Consent -->|Opted in| Render["Render Template\n(variable substitution,\nlanguage selection)"]
    Consent -->|Opted out| Skip["Skip (log suppression)"]
    Render --> Queue["Kafka Queue\n(per channel)"]

    Queue --> EmailWorker["Email Worker\n(SendGrid/Mailgun)"]
    Queue --> SMSWorker["SMS Worker\n(Twilio/Netgsm)"]
    Queue --> WhatsAppWorker["WhatsApp Worker\n(Business API)"]
    Queue --> PushWorker["Push Worker\n(FCM/APNS)"]

    EmailWorker --> Track["Track Delivery\n(webhook callbacks)"]
    SMSWorker --> Track
    WhatsAppWorker --> Track
    PushWorker --> Track

    Track --> DB[("Update\nNotificationRecipient\nstatus")]

    style Event fill:#8e44ad,color:#fff
    style Queue fill:#231f20,color:#fff
    style Track fill:#27ae60,color:#fff
```

## Use Cases

### UC-NOT-001: Send Transactional Notification
- **Actor**: System (event-driven)
- **Flow**:
  1. Module publishes event (e.g., `donations.donation.confirmed`)
  2. Handler resolves notification template by event code + channel
  3. Handler resolves recipient(s) from event payload
  4. Check contact's communication preference for channel
  5. If opted in: render template with variables (donor name, amount, etc.)
  6. Select language based on contact preference
  7. Queue message for delivery
  8. Worker sends via configured provider
  9. Track delivery status via provider webhooks
- **Business Rules**:
  - Transactional notifications bypass marketing consent (e.g., receipts, password reset)
  - Marketing notifications require explicit consent
  - Rate limit: max 1 notification per contact per event per hour (dedup)

### UC-NOT-002: Bulk Notification (Marketing)
- **Actor**: CRM Campaign module
- **Flow**:
  1. CRM creates campaign with segment + template
  2. CRM sends `crm.campaign.dispatch` event with recipient list
  3. Notification engine validates consent for all recipients
  4. Filters out opted-out contacts
  5. Sends in batches with throttling (configurable rate)
  6. Tracks per-recipient delivery + opens + clicks
  7. Reports results back to CRM campaign analytics
- **Business Rules**:
  - Throttling: max N messages/minute (configurable per provider)
  - Unsubscribe link auto-appended to all marketing emails
  - Time-zone aware scheduling (send at 10am recipient's local time)

## API Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/v1/notifications/send` | Send ad-hoc notification | `notifications.send` |
| GET | `/api/v1/notifications/notifications` | List sent notifications | `notifications.read` |
| GET | `/api/v1/notifications/notifications/{id}` | Get delivery details | `notifications.read` |
| GET | `/api/v1/notifications/templates` | List templates | `notifications.templates.read` |
| POST | `/api/v1/notifications/templates` | Create template | `notifications.templates.manage` |
| PUT | `/api/v1/notifications/templates/{id}` | Update template | `notifications.templates.manage` |
| GET | `/api/v1/notifications/providers` | List providers | `notifications.providers.read` |
| PUT | `/api/v1/notifications/providers/{id}` | Configure provider | `notifications.providers.manage` |
| POST | `/api/v1/notifications/webhooks/{provider}` | Provider delivery webhook | Provider signature |

## Integration Points

### Events Consumed (from all modules)
| Event | Source | Action |
|-------|--------|--------|
| `donations.donation.confirmed` | Donations | Send receipt + thank you |
| `donations.recurring.payment_failed` | Donations | Alert donor |
| `crm.lead.assigned` | CRM | Notify assignee |
| `crm.campaign.dispatch` | CRM | Bulk send campaign |
| `education.appointment.booked` | Education | Send confirmation + calendar invite |
| `education.enrollment.accepted` | Education | Send acceptance letter |
| `sponsorship.update.sent` | Sponsorship | Send update to sponsor |
| `sponsorship.installment.overdue` | Sponsorship | Send reminder |
| `identity.user.created` | Identity | Send welcome / invitation email |

### Events Produced
| Event | Topic |
|-------|-------|
| `notifications.notification.delivered` | `nexora.notifications` |
| `notifications.notification.bounced` | `nexora.notifications` |
| `notifications.notification.opened` | `nexora.notifications` |

## Non-Functional Requirements

| Requirement | Target |
|------------|--------|
| Transactional delivery | < 30 seconds |
| Bulk throughput | 1,000 messages/minute |
| Email open tracking | Pixel tracking (< 1ms response) |
| Provider failover | Auto-switch to backup provider on failure |
| Retry policy | 3 retries with exponential backoff |
| Template rendering | < 50ms |
