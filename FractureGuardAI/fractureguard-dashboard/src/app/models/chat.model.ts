export interface ChatMessage {
  id?: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
}

export interface AlertReport {
  content: string;
  risk_pct?: number;
  session_id: string;
}
