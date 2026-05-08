import { TestBed } from '@angular/core/testing';
import { AlertService, ALERT_SOCKET } from './alert.service';

const mockSocket = { on: jasmine.createSpy('on') };

describe('AlertService', () => {
  let service: AlertService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: ALERT_SOCKET, useValue: mockSocket },
      ],
    });
    service = TestBed.inject(AlertService);
  });

  it('starts with no alert', () => {
    expect(service.latestAlert()).toBeNull();
  });

  it('setAlert updates the signal', () => {
    service.setAlert({ content: 'Risk: 85%', session_id: 'abc' });
    expect(service.latestAlert()?.content).toBe('Risk: 85%');
  });

  it('clearAlert resets to null', () => {
    service.setAlert({ content: 'Risk: 85%', session_id: 'abc' });
    service.clearAlert();
    expect(service.latestAlert()).toBeNull();
  });
});
