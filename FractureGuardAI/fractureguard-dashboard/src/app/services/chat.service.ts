import { Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { ChatMessage } from '../models/chat.model';

const DEV_JWT = 'dev-engineer-token';

@Injectable({ providedIn: 'root' })
export class ChatService {
  readonly messages    = signal<ChatMessage[]>([]);
  readonly isStreaming = signal(false);

  private sessionId = crypto.randomUUID();

  addUserMessage(content: string): void {
    this.messages.update(msgs => [
      ...msgs,
      { role: 'user', content, timestamp: new Date().toISOString() }
    ]);
  }

  async sendMessage(content: string): Promise<void> {
    this.addUserMessage(content);
    this.isStreaming.set(true);

    const assistantMsg: ChatMessage = {
      role: 'assistant', content: '', timestamp: new Date().toISOString()
    };
    this.messages.update(msgs => [...msgs, assistantMsg]);
    const msgIndex = this.messages().length - 1;

    try {
      const response = await fetch(`${environment.apiUrl}/api/chat`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${DEV_JWT}`,
        },
        body: JSON.stringify({ message: content, sessionId: this.sessionId }),
      });

      const reader  = response.body!.getReader();
      const decoder = new TextDecoder();

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        const chunk = decoder.decode(value);
        const lines = chunk.split('\n').filter(l => l.startsWith('data: '));
        for (const line of lines) {
          const text = line.slice(6);
          this.messages.update(msgs =>
            msgs.map((m, i) => i === msgIndex ? { ...m, content: m.content + text } : m)
          );
        }
      }
    } finally {
      this.isStreaming.set(false);
    }
  }
}
