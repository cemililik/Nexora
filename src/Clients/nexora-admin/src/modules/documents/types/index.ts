// Enums as union types
export type DocumentStatus = 'Active' | 'Archived' | 'Deleted' | 'PendingRender';
export type TemplateCategory = 'Contract' | 'Receipt' | 'Letter' | 'Report';
export type TemplateFormat = 'Docx' | 'Pdf' | 'Html';
export type AccessPermission = 'View' | 'Edit' | 'Manage';
export type SignatureRequestStatus =
  | 'Draft'
  | 'Sent'
  | 'PartiallySigned'
  | 'Completed'
  | 'Cancelled'
  | 'Expired';
export type SignatureRecipientStatus =
  | 'Pending'
  | 'Viewed'
  | 'Signed'
  | 'Declined'
  | 'Expired';

// Document DTOs
export interface DocumentDto {
  id: string;
  folderId: string;
  name: string;
  description?: string;
  mimeType: string;
  fileSize: number;
  storageKey: string;
  status: DocumentStatus;
  linkedEntityId?: string;
  linkedEntityType?: string;
  currentVersion: number;
  createdAt: string;
}

export interface DocumentDetailDto extends DocumentDto {
  folderName: string;
  tags?: string;
  uploadedByUserId: string;
  updatedAt?: string;
  versions: DocumentVersionDto[];
  accessList: DocumentAccessDto[];
}

export interface DocumentVersionDto {
  id: string;
  versionNumber: number;
  storageKey: string;
  fileSize: number;
  changeNote?: string;
  uploadedByUserId: string;
  createdAt: string;
}

export interface DocumentAccessDto {
  id: string;
  userId?: string;
  roleId?: string;
  permission: AccessPermission;
}

// Folder DTOs
export interface FolderDto {
  id: string;
  name: string;
  path: string;
  parentFolderId?: string;
  moduleName?: string;
  isSystem: boolean;
  createdAt: string;
}

// Template DTOs
export interface DocumentTemplateDto {
  id: string;
  name: string;
  category: TemplateCategory;
  format: TemplateFormat;
  isActive: boolean;
  createdAt: string;
}

export interface DocumentTemplateDetailDto extends DocumentTemplateDto {
  templateStorageKey: string;
  variableDefinitions?: string;
  updatedAt?: string;
}

// Signature DTOs
export interface SignatureRequestDto {
  id: string;
  documentId: string;
  title: string;
  status: SignatureRequestStatus;
  expiresAt?: string;
  recipientCount: number;
  signedCount: number;
  createdAt: string;
}

export interface SignatureRequestDetailDto {
  id: string;
  documentId: string;
  title: string;
  status: SignatureRequestStatus;
  expiresAt?: string;
  completedAt?: string;
  createdByUserId: string;
  createdAt: string;
  recipients: SignatureRecipientDto[];
}

export interface SignatureRecipientDto {
  id: string;
  contactId: string;
  email: string;
  name: string;
  signingOrder: number;
  status: SignatureRecipientStatus;
  signedAt?: string;
}

// Upload/Download DTOs
export interface UploadUrlDto {
  uploadUrl: string;
  storageKey: string;
  expiresAt: string;
}

export interface DownloadUrlDto {
  downloadUrl: string;
  expiresAt: string;
}

export interface RenderTemplateResultDto {
  documentId: string;
  name: string;
  storageKey: string;
}

// Request types
export interface GenerateUploadUrlRequest {
  fileName: string;
  contentType: string;
  fileSize: number;
}

export interface ConfirmUploadRequest {
  folderId: string;
  storageKey: string;
  name: string;
  mimeType: string;
  fileSize: number;
  description?: string;
  linkedEntityId?: string;
  linkedEntityType?: string;
  tags?: string;
}

export interface UpdateDocumentMetadataRequest {
  name: string;
  description?: string;
  tags?: string;
}

export interface MoveDocumentRequest {
  targetFolderId: string;
}

export interface LinkDocumentRequest {
  entityId: string;
  entityType: string;
}

export interface AddVersionRequest {
  storageKey: string;
  fileSize: number;
  changeNote?: string;
}

export interface GrantAccessRequest {
  userId?: string;
  roleId?: string;
  permission: AccessPermission;
}

export interface CreateFolderRequest {
  name: string;
  parentFolderId?: string;
  moduleName?: string;
}

export interface RenameFolderRequest {
  newName: string;
}

export interface CreateDocumentTemplateRequest {
  name: string;
  category: TemplateCategory;
  format: TemplateFormat;
  templateStorageKey: string;
  variableDefinitions?: string;
}

export interface UpdateDocumentTemplateRequest {
  name: string;
  category: TemplateCategory;
  format: TemplateFormat;
  variableDefinitions?: string;
}

export interface RenderTemplateRequest {
  folderId: string;
  outputName: string;
  variables: Record<string, string>;
}

export interface SignatureRecipientInput {
  contactId: string;
  email: string;
  name: string;
  signingOrder: number;
}

export interface CreateSignatureRequestRequest {
  documentId: string;
  title: string;
  expiresAt?: string;
  recipients: SignatureRecipientInput[];
}

export interface SignRequest {
  recipientId: string;
  signatureData: string;
}

export interface DeclineRequest {
  recipientId: string;
}
