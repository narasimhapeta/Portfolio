import { Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { ChatMessage } from '../models/chat.model';

const DEV_JWT = 'dev-engineer-token'; // TODO: replace with MSAL token in prod

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
    if (this.isStreaming()) return; // guard against concurrent sends

    this.addUserMessage(content);
    this.isStreaming.set(true);

    // Use a stable ID to avoid fragile index-based updates
    const msgId = crypto.randomUUID();
    const assistantMsg: ChatMessage = {
      role: 'assistant', content: '', timestamp: new Date().toISOString(),
      id: msgId,
    };
    this.messages.update(msgs => [...msgs, assistantMsg]);

    try {
      const response = await fetch(`${environment.apiUrl}/api/chat`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${DEV_JWT}`,
        },
        body: JSON.stringify({ message: content, sessionId: this.sessionId }),
      });

      if (!response.ok) {
        throw new Error(`Chat API returned ${response.status}`);
      }

      if (!response.body) {
        throw new Error('Streaming not supported by this browser');
      }

      const reader  = response.body.getReader();
      const decoder = new TextDecoder();

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        const chunk = decoder.decode(value);
        const lines = chunk.split('\n').filter(l => l.startsWith('data: '));
        for (const line of lines) {
          const text = line.slice(6);
          this.messages.update(msgs =>
            msgs.map(m => m.id === msgId ? { ...m, content: m.content + text } : m)
          );
        }
      }
    } catch (err) {
      const errorText = err instanceof Error ? err.message : 'Unknown error';
      this.messages.update(msgs =>
        msgs.map(m => m.id === msgId ? { ...m, content: `[Error: ${errorText}]` } : m)
      );
    } finally {
      this.isStreaming.set(false);
    }
  }
}
