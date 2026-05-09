import { ChatMessage, ChatRequest } from '../types/chat';
import apiClient from './apiClient';

export const sendMessage = async (request: ChatRequest): Promise<ChatMessage> => {
  const res = await apiClient.post('/ai/chat', request);
  return res.data;
};

export const getChatHistory = async (customerId = 'cust-001'): Promise<ChatMessage[]> => {
  const res = await apiClient.get('/ai/chat/history', { params: { customerId } });
  return res.data.history;
};

export const resetChat = async (): Promise<void> => {
  await apiClient.post('/ai/reset');
};
