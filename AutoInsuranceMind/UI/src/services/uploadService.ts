import { UploadedDocument } from '../types/upload';
import apiClient from './apiClient';

export const uploadDocument = async (
  file: File,
  customerId = 'cust-001',
  policyId?: string
): Promise<UploadedDocument> => {
  const formData = new FormData();
  formData.append('file', file);
  const res = await apiClient.post('/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    params: { customerId, policyId },
  });
  return res.data.document;
};

export const getDocuments = async (customerId = 'cust-001'): Promise<UploadedDocument[]> => {
  const res = await apiClient.get('/upload/documents', { params: { customerId } });
  return res.data.documents;
};

export const deleteDocument = async (id: string): Promise<void> => {
  await apiClient.delete(`/upload/documents/${id}`);
};
