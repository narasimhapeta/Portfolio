import { Injectable, InjectionToken, inject, signal } from '@angular/core';
import { io, Socket } from 'socket.io-client';
import { environment } from '../../environments/environment';
import { SensorReading } from '../models/sensor.model';

const PRESSURE_DANGER_THRESHOLD = 950;

export const TELEMETRY_SOCKET = new InjectionToken<Socket>('TELEMETRY_SOCKET', {
  providedIn: 'root',
  factory: () => io(environment.notifierUrl, { transports: ['websocket'] }),
});

@Injectable({ providedIn: 'root' })
export class TelemetryService {
  readonly latestReading = signal<SensorReading | null>(null);
  readonly history       = signal<SensorReading[]>([]);

  private socket = inject(TELEMETRY_SOCKET);

  constructor() {
    this.socket.on('sensor:reading', (reading: SensorReading) => {
      this.latestReading.set(reading);
      this.history.update(h => [...h.slice(-59), reading]);
    });
  }

  isAtRisk(reading: SensorReading): boolean {
    return reading.pressure_psi > PRESSURE_DANGER_THRESHOLD;
  }
}
