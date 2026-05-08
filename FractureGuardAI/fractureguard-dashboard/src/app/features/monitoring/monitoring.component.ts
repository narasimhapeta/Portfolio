import { Component, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TelemetryService } from '../../services/telemetry.service';
import { AlertService } from '../../services/alert.service';
import { ChatPanelComponent } from '../chat/chat-panel.component';

@Component({
  selector: 'app-monitoring',
  standalone: true,
  imports: [CommonModule, ChatPanelComponent],
  templateUrl: './monitoring.component.html',
})
export class MonitoringComponent {
  protected telemetry  = inject(TelemetryService);
  protected alerts     = inject(AlertService);

  protected reading    = this.telemetry.latestReading;
  protected atRisk     = computed(() => {
    const r = this.reading();
    return r ? this.telemetry.isAtRisk(r) : false;
  });
  protected activeAlert = this.alerts.latestAlert;
  protected showChat    = false;

  toggleChat() { this.showChat = !this.showChat; }
  dismissAlert() { this.alerts.clearAlert(); }
}
