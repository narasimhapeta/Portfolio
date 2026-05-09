import React, { useState, useRef, useEffect } from 'react';
import { ChatMessage } from '../types/chat';
import { sendMessage, resetChat } from '../services/chatService';
import '../styles/ChatBot.css';

const SUGGESTIONS = [
  'What are my coverage limits?',
  'When does my policy expire?',
  'What is my annual premium?',
  'How do I file a claim?',
];

const ChatBot: React.FC = () => {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, loading]);

  const handleSend = async (text?: string) => {
    const messageText = text ?? input.trim();
    if (!messageText || loading) return;

    setInput('');
    setError('');
    setLoading(true);

    const optimisticMsg: ChatMessage = {
      id: `temp-${Date.now()}`,
      customerId: 'cust-001',
      message: messageText,
      response: '',
      sources: [],
      usedRag: false,
      timestamp: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, optimisticMsg]);

    try {
      const response = await sendMessage({ message: messageText, customerId: 'cust-001' });
      setMessages((prev) => prev.map((m) => (m.id === optimisticMsg.id ? response : m)));
    } catch (err: any) {
      setMessages((prev) => prev.filter((m) => m.id !== optimisticMsg.id));
      setError(err.message ?? 'Failed to send message');
    } finally {
      setLoading(false);
    }
  };

  const handleReset = async () => {
    await resetChat();
    setMessages([]);
    setError('');
  };

  return (
    <div className="chatbot">
      <div className="chat-header">
        <div>
          <h3>🤖 AI Insurance Assistant</h3>
          <p className="chat-subtitle">Powered by Semantic Kernel + RAG</p>
        </div>
        {messages.length > 0 && (
          <button className="btn-link btn-sm" onClick={handleReset}>Clear</button>
        )}
      </div>

      {messages.length === 0 && (
        <div className="chat-suggestions">
          <p className="suggestions-label">Try asking:</p>
          <div className="suggestions-grid">
            {SUGGESTIONS.map((s) => (
              <button key={s} className="suggestion-chip" onClick={() => handleSend(s)}>
                {s}
              </button>
            ))}
          </div>
        </div>
      )}

      <div className="messages-container">
        {messages.map((msg) => (
          <div key={msg.id} className="message-pair">
            <div className="message user-message">
              <span className="message-avatar">👤</span>
              <div className="message-content">{msg.message}</div>
            </div>
            {msg.response && (
              <div className="message ai-message">
                <span className="message-avatar">🤖</span>
                <div className="message-content">
                  {msg.response}
                  {msg.sources.length > 0 && (
                    <div className="rag-sources">
                      📎 Sources: {msg.sources.join(', ')}
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        ))}
        {loading && (
          <div className="message ai-message">
            <span className="message-avatar">🤖</span>
            <div className="message-content typing-indicator">
              <span /><span /><span />
            </div>
          </div>
        )}
        {error && <p className="chat-error">{error}</p>}
        <div ref={messagesEndRef} />
      </div>

      <div className="chat-input-area">
        <input
          className="chat-input"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && !e.shiftKey && handleSend()}
          placeholder="Ask about your policy, coverage, or claims…"
          disabled={loading}
        />
        <button
          className="btn-send"
          onClick={() => handleSend()}
          disabled={loading || !input.trim()}
        >
          Send
        </button>
      </div>
    </div>
  );
};

export default ChatBot;
