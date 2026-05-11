import { Component, inject, signal, AfterViewChecked, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatService } from '../../services/chat.service';

@Component({
  selector: 'app-chat-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-panel.component.html',
  styleUrl: './chat-panel.component.css',
})
export class ChatPanelComponent implements AfterViewChecked {
  protected chat      = inject(ChatService);
  protected inputText = signal('');

  @ViewChild('msgContainer') private msgContainer!: ElementRef<HTMLElement>;

  readonly suggestions = [
    'What is the current sensor status?',
    'What is the risk of a screen-out in the next hour?',
    'Explain the current pressure trend',
    'Are vibration levels within safe limits?',
  ];

  ngAfterViewChecked(): void {
    const el = this.msgContainer?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
  }

  async send(): Promise<void> {
    const text = this.inputText().trim();
    if (!text) return;
    this.inputText.set('');
    await this.chat.sendMessage(text);
  }

  async sendSuggestion(text: string): Promise<void> {
    await this.chat.sendMessage(text);
  }
}
