import { TestBed } from '@angular/core/testing';
import { TelemetryService, TELEMETRY_SOCKET } from './telemetry.service';

const mockSocket = { on: jasmine.createSpy('on') };

describe('TelemetryService', () => {
  let service: TelemetryService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: TELEMETRY_SOCKET, useValue: mockSocket },
      ],
    });
    service = TestBed.inject(TelemetryService);
  });

  it('starts with null reading', () => {
    expect(service.latestReading()).toBeNull();
  });

  it('isAtRisk returns true when pressure exceeds danger threshold', () => {
    expect(service.isAtRisk({
      pressure_psi: 1050, flow_rate_bpm: 10,
      vibration_g: 1, temperature_c: 40, timestamp: ''
    })).toBeTrue();
  });

  it('isAtRisk returns false for normal readings', () => {
    expect(service.isAtRisk({
      pressure_psi: 600, flow_rate_bpm: 10,
      vibration_g: 1, temperature_c: 40, timestamp: ''
    })).toBeFalse();
  });
});
