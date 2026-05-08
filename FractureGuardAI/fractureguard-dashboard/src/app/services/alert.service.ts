import { Injectable, InjectionToken, inject, signal } from '@angular/core';
import { io, Socket } from 'socket.io-client';
import { environment } from '../../environments/environment';
import { AlertReport } from '../models/chat.model';

export const ALERT_SOCKET = new InjectionToken<Socket>('ALERT_SOCKET', {
  providedIn: 'root',
  factory: () => io(environment.notifierUrl, { transports: ['websocket'] }),
});

@Injectable({ providedIn: 'root' })
export class AlertService {
  readonly latestAlert = signal<AlertReport | null>(null);

  private socket = inject(ALERT_SOCKET);

  constructor() {
    this.socket.on('alert:report', (report: AlertReport) => {
      this.latestAlert.set(report);
    });
  }

  setAlert(alert: AlertReport): void {
    this.latestAlert.set(alert);
  }

  clearAlert(): void {
    this.latestAlert.set(null);
  }
}
