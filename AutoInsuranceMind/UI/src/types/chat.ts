export interface ChatMessage {
  id: string;
  customerId: string;
  message: string;
  response: string;
  sources: string[];
  usedRag: boolean;
  timestamp: string;
}

export interface ChatRequest {
  message: string;
  customerId?: string;
  documentId?: string;
}
