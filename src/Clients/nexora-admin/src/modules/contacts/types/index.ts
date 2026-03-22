// Enums as union types
export type ContactType = 'Individual' | 'Organization';
export type ContactStatus = 'Active' | 'Archived' | 'Merged';
export type ContactSource = 'WebForm' | 'Import' | 'Manual' | 'Api';
export type AddressType = 'Home' | 'Work' | 'Billing' | 'Shipping';
export type ConsentType = 'EmailMarketing' | 'SmsMarketing' | 'DataProcessing';
export type CommunicationChannel = 'Email' | 'Sms' | 'WhatsApp' | 'Phone' | 'Mail';
export type RelationshipType =
  | 'ParentOf'
  | 'ChildOf'
  | 'SpouseOf'
  | 'SiblingOf'
  | 'EmployeeOf'
  | 'EmployerOf'
  | 'ContactOf'
  | 'GuardianOf'
  | 'WardOf';
export type TagCategory = 'Donor' | 'Parent' | 'Volunteer' | 'Vendor' | 'Student' | 'Staff';
export type ImportJobStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed';
export type ExportFormat = 'csv' | 'xlsx';

// DTOs
export interface ContactDto {
  id: string;
  type: ContactType;
  title?: string;
  firstName?: string;
  lastName?: string;
  displayName: string;
  companyName?: string;
  email?: string;
  phone?: string;
  source: ContactSource;
  status: ContactStatus;
  createdAt: string;
}

export interface ContactDetailDto extends ContactDto {
  mobile?: string;
  website?: string;
  taxId?: string;
  language: string;
  currency: string;
  mergedIntoId?: string;
  addresses: ContactAddressDto[];
  tags: ContactTagSummaryDto[];
}

export interface ContactAddressDto {
  id: string;
  type: AddressType;
  street1: string;
  street2?: string;
  city: string;
  state?: string;
  postalCode?: string;
  countryCode: string;
  isPrimary: boolean;
}

export interface ContactTagSummaryDto {
  tagId: string;
  name: string;
  category: TagCategory;
  color?: string;
}

export interface TagDto {
  id: string;
  name: string;
  category: TagCategory;
  color?: string;
  isActive: boolean;
  createdAt: string;
}

export interface ContactTagDto {
  contactTagId: string;
  contactId: string;
  tagId: string;
  tagName: string;
  tagCategory: TagCategory;
  tagColor?: string;
  assignedAt: string;
}

export interface ContactRelationshipDto {
  id: string;
  contactId: string;
  relatedContactId: string;
  relatedContactDisplayName: string;
  type: RelationshipType;
  createdAt: string;
}

export interface ContactNoteDto {
  id: string;
  contactId: string;
  authorUserId: string;
  content: string;
  isPinned: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface ConsentRecordDto {
  id: string;
  contactId: string;
  consentType: ConsentType;
  granted: boolean;
  source?: string;
  grantedAt: string;
  revokedAt?: string;
}

export interface CommunicationPreferenceDto {
  id: string;
  contactId: string;
  channel: CommunicationChannel;
  optedIn: boolean;
  optedInAt?: string;
  optedOutAt?: string;
  optInSource?: string;
}

export interface ContactActivityDto {
  id: string;
  contactId: string;
  moduleSource: string;
  activityType: string;
  summary: string;
  details?: string;
  occurredAt: string;
}

export interface CustomFieldDefinitionDto {
  id: string;
  fieldName: string;
  fieldType: string;
  options?: string;
  isRequired: boolean;
  displayOrder: number;
  isActive: boolean;
  createdAt: string;
}

export interface ContactCustomFieldDto {
  id: string;
  contactId: string;
  fieldDefinitionId: string;
  fieldName: string;
  fieldType: string;
  value?: string;
}

export interface DuplicateContactDto {
  contactId: string;
  displayName: string;
  email?: string;
  phone?: string;
  type: ContactType;
  status: ContactStatus;
  score: number;
}

export interface MergeResultDto {
  primaryContactId: string;
  secondaryContactId: string;
  primaryDisplayName: string;
}

export interface Contact360Dto {
  contact: ContactDetailDto;
  relationships: ContactRelationshipDto[];
  communicationPreferences: CommunicationPreferenceDto[];
  recentNotes: ContactNoteDto[];
  consentRecords: ConsentRecordDto[];
  recentActivities: ContactActivityDto[];
  customFields: ContactCustomFieldDto[];
}

export interface ImportJobDto {
  jobId: string;
  status: ImportJobStatus;
  totalRows: number;
  processedRows: number;
  successCount: number;
  errorCount: number;
  createdAt: string;
  completedAt?: string;
}

export interface ExportJobDto {
  jobId: string;
  status: string;
  format: ExportFormat;
  createdAt: string;
  completedAt?: string;
  downloadUrl?: string;
}

// Request types
export interface CreateContactRequest {
  type: ContactType;
  title?: string;
  firstName?: string;
  lastName?: string;
  companyName?: string;
  email?: string;
  phone?: string;
  source: ContactSource;
}

export interface UpdateContactRequest {
  title?: string;
  firstName?: string;
  lastName?: string;
  companyName?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  website?: string;
  taxId?: string;
  language: string;
  currency: string;
}

export interface AddAddressRequest {
  type: AddressType;
  street1: string;
  street2?: string;
  city: string;
  state?: string;
  postalCode?: string;
  countryCode: string;
  isPrimary: boolean;
}

export interface UpdateAddressRequest {
  type: AddressType;
  street1: string;
  street2?: string;
  city: string;
  state?: string;
  postalCode?: string;
  countryCode: string;
}

export interface CreateTagRequest {
  name: string;
  category: TagCategory;
  color?: string;
}

export interface UpdateTagRequest {
  name: string;
  category: TagCategory;
  color?: string;
}

export interface AddRelationshipRequest {
  relatedContactId: string;
  type: RelationshipType;
}

export interface RecordConsentRequest {
  consentType: ConsentType;
  granted: boolean;
  source?: string;
}

export interface MergeContactsRequest {
  primaryContactId: string;
  secondaryContactId: string;
  useSecondaryEmail: boolean;
  useSecondaryPhone: boolean;
}

export interface LogActivityRequest {
  moduleSource: string;
  activityType: string;
  summary: string;
  details?: string;
}

export interface GenerateImportUploadUrlRequest {
  fileName: string;
  contentType: string;
  fileSize: number;
}

export interface ImportUploadUrlDto {
  uploadUrl: string;
  storageKey: string;
  expiresAt: string;
}

export interface ConfirmImportRequest {
  fileName: string;
  fileFormat: string;
  storageKey: string;
}

export interface StartExportRequest {
  format: ExportFormat;
  statusFilter?: ContactStatus;
  typeFilter?: ContactType;
}

export interface CreateCustomFieldRequest {
  fieldName: string;
  fieldType: string;
  options?: string;
  isRequired: boolean;
  displayOrder: number;
}

export interface UpdateCustomFieldRequest {
  fieldName: string;
  options?: string;
  isRequired: boolean;
  displayOrder: number;
}

export interface SetCustomFieldValueRequest {
  value?: string;
}

export interface ChannelPreferenceRequest {
  channel: CommunicationChannel;
  optedIn: boolean;
  optInSource?: string;
}

export interface UpdatePreferencesRequest {
  preferences: ChannelPreferenceRequest[];
}

export interface AddNoteRequest {
  content: string;
}

export interface UpdateNoteRequest {
  content: string;
}

export interface PinNoteRequest {
  pin: boolean;
}

export interface GdprDeleteRequest {
  reason: string;
}
