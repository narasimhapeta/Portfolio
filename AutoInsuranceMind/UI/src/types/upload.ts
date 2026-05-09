export interface UploadedDocument {
  id: string;
  customerId: string;
  policyId?: string;
  fileName: string;
  fileType: string;
  fileSize: number;
  status: string;
  uploadedAt: string;
}
