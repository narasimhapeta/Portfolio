import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ChatService } from './chat.service';

describe('ChatService', () => {
  let service: ChatService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ChatService);
  });

  it('starts with empty messages', () => {
    expect(service.messages().length).toBe(0);
  });

  it('addUserMessage appends a user message', () => {
    service.addUserMessage('What is the screen-out risk?');
    expect(service.messages().length).toBe(1);
    expect(service.messages()[0].role).toBe('user');
    expect(service.messages()[0].content).toBe('What is the screen-out risk?');
  });

  it('isStreaming starts false', () => {
    expect(service.isStreaming()).toBeFalse();
  });
});
