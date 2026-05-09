# Phase 4 — Angular 19 Dashboard

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Angular 19 operator dashboard — a Monitoring-First SPA that streams live sensor data via Socket.io, lets engineers converse with the AI via a collapsible chat panel, and displays live alert banners when ML reports arrive.

**Architecture:** Standalone components throughout (no NgModules). Angular Signals drive all reactive state. Three services (TelemetryService, ChatService, AlertService) each own a Socket.io or HTTP connection. The main MonitoringComponent shows KPI cards and charts by default; chat panel is collapsible.

**Tech Stack:** Angular 19 · MSAL Angular 3 · Socket.io-client 4 · ApexCharts · ng-apexcharts · Angular CLI 19

**Depends on:** Phase 0 (scaffold), Phase 2 (Node.js Notifier for WebSocket), Phase 3 (.NET API for chat endpoint)

---

## File Map

```
fractureguard-dashboard/
├── angular.json
├── package.json
└── src/
    ├── app/
    │   ├── app.config.ts            Standalone app config, providers
    │   ├── app.routes.ts            Lazy-loaded routes
    │   ├── features/
    │   │   ├── monitoring/
    │   │   │   ├── monitoring.component.ts     Main layout, KPI cards, chart, chat toggle
    │   │   │   ├── monitoring.component.html
    │   │   │   └── monitoring.component.spec.ts
    │   │   ├── chat/
    │   │   │   ├── chat-panel.component.ts     SSE streaming chat UI
    │   │   │   └── chat-panel.component.html
    │   │   └── reports/
    │   │       └── reports.component.ts        Historical reports list
    │   ├── services/
    │   │   ├── telemetry.service.ts    Socket.io sensor stream → signal
    │   │   ├── telemetry.service.spec.ts
    │   │   ├── chat.service.ts         fetch + SSE → streaming signal
    │   │   ├── chat.service.spec.ts
    │   │   ├── alert.service.ts        Socket.io alert:report → signal
    │   │   └── alert.service.spec.ts
    │   └── models/
    │       ├── sensor.model.ts
    │       └── chat.model.ts
    └── environments/
        ├── environment.ts
        └── environment.prod.ts
```

---

### Task 11: Angular project setup + routing + models

**Files:**
- Create: `fractureguard-dashboard/` (via Angular CLI)
- Modify: `fractureguard-dashboard/src/app/app.config.ts`
- Create: `fractureguard-dashboard/src/app/app.routes.ts`
- Create: `fractureguard-dashboard/src/environments/environment.ts`
- Create: `fractureguard-dashboard/src/app/models/sensor.model.ts`
- Create: `fractureguard-dashboard/src/app/models/chat.model.ts`

- [ ] **Step 1: Generate Angular project**

```bash
npx @angular/cli@19 new fractureguard-dashboard \
  --standalone \
  --routing \
  --style css \
  --skip-git \
  --skip-tests false
cd fractureguard-dashboard
npm install @azure/msal-browser @azure/msal-angular apexcharts ng-apexcharts socket.io-client
```

Expected: Project created, all packages installed without errors.

- [ ] **Step 2: Create `src/environments/environment.ts`**

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000',
  notifierUrl: 'http://localhost:3001',
  msalConfig: {
    auth: {
      clientId: 'dev-client-id',
      authority: 'https://login.microsoftonline.com/dev-tenant',
      redirectUri: 'http://localhost:4200',
    }
  },
  devMode: true,   // skips real MSAL flow, uses hardcoded dev JWT
};
```

- [ ] **Step 3: Create `src/environments/environment.prod.ts`**

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://api.fractureguard.example.com',
  notifierUrl: 'https://notifier.fractureguard.example.com',
  msalConfig: {
    auth: {
      clientId: '__MSAL_CLIENT_ID__',       // injected by CI/CD
      authority: '__MSAL_AUTHORITY__',
      redirectUri: 'https://fractureguard.example.com',
    }
  },
  devMode: false,
};
```

- [ ] **Step 4: Create `src/app/models/sensor.model.ts`**

```typescript
export interface SensorReading {
  pressure_psi: number;
  flow_rate_bpm: number;
  vibration_g: number;
  temperature_c: number;
  timestamp: string;
}
```

- [ ] **Step 5: Create `src/app/models/chat.model.ts`**

```typescript
export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
}

export interface AlertReport {
  content: string;
  risk_pct?: number;
  session_id: string;
}
```

- [ ] **Step 6: Update `src/app/app.config.ts`**

```typescript
import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(),
  ],
};
```

- [ ] **Step 7: Create `src/app/app.routes.ts`**

```typescript
import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/monitoring/monitoring.component')
        .then(m => m.MonitoringComponent),
  },
  {
    path: 'reports',
    loadComponent: () =>
      import('./features/reports/reports.component')
        .then(m => m.ReportsComponent),
  },
  { path: '**', redirectTo: '' },
];
```

- [ ] **Step 8: Build to confirm project setup**

```bash
npm run build
```

Expected: Build at `dist/fractureguard-dashboard` — 0 errors.

- [ ] **Step 9: Commit**

```bash
git add fractureguard-dashboard/
git commit -m "feat(dashboard): Angular 19 project setup with routing, models and environments"
```

---

### Task 12: TelemetryService + MonitoringComponent

**Files:**
- Create: `src/app/services/telemetry.service.ts`
- Create: `src/app/features/monitoring/monitoring.component.ts`
- Create: `src/app/features/monitoring/monitoring.component.html`
- Test: `src/app/services/telemetry.service.spec.ts`

- [ ] **Step 1: Write failing test**

```typescript
// src/app/services/telemetry.service.spec.ts
import { TestBed } from '@angular/core/testing';
import { TelemetryService } from './telemetry.service';

describe('TelemetryService', () => {
  let service: TelemetryService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
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
```

- [ ] **Step 2: Run — expect FAIL**

```bash
ng test --include="**/telemetry.service.spec.ts" --watch=false
```

Expected: Compile error — `TelemetryService` not found.

- [ ] **Step 3: Create `src/app/services/telemetry.service.ts`**

```typescript
import { Injectable, signal } from '@angular/core';
import { io, Socket } from 'socket.io-client';
import { environment } from '../../environments/environment';
import { SensorReading } from '../models/sensor.model';

const PRESSURE_DANGER_THRESHOLD = 950;

@Injectable({ providedIn: 'root' })
export class TelemetryService {
  readonly latestReading = signal<SensorReading | null>(null);
  readonly history       = signal<SensorReading[]>([]);

  private socket: Socket;

  constructor() {
    this.socket = io(environment.notifierUrl, { transports: ['websocket'] });
    this.socket.on('sensor:reading', (reading: SensorReading) => {
      this.latestReading.set(reading);
      this.history.update(h => [...h.slice(-59), reading]); // rolling 60-point window
    });
  }

  isAtRisk(reading: SensorReading): boolean {
    return reading.pressure_psi > PRESSURE_DANGER_THRESHOLD;
  }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
ng test --include="**/telemetry.service.spec.ts" --watch=false
```

Expected: All 3 tests PASS.

- [ ] **Step 5: Create `src/app/features/monitoring/monitoring.component.ts`**

```typescript
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
```

- [ ] **Step 6: Create `src/app/features/monitoring/monitoring.component.html`**

```html
<div class="dashboard" [class.at-risk]="atRisk()">

  <!-- Live alert banner -->
  @if (activeAlert()) {
    <div class="alert-banner">
      <strong>AI RISK ALERT</strong>
      {{ activeAlert()?.content }}
      <button class="dismiss" (click)="dismissAlert()">✕</button>
    </div>
  }

  <!-- KPI sensor cards -->
  @if (reading(); as r) {
    <div class="kpi-grid">
      <div class="kpi-card" [class.danger]="r.pressure_psi > 950">
        <span class="label">Pressure</span>
        <span class="value">{{ r.pressure_psi | number:'1.0-0' }} PSI</span>
      </div>
      <div class="kpi-card">
        <span class="label">Flow Rate</span>
        <span class="value">{{ r.flow_rate_bpm | number:'1.1-1' }} BPM</span>
      </div>
      <div class="kpi-card" [class.danger]="r.vibration_g > 2.5">
        <span class="label">Vibration</span>
        <span class="value">{{ r.vibration_g | number:'1.2-2' }} g</span>
      </div>
      <div class="kpi-card">
        <span class="label">Temperature</span>
        <span class="value">{{ r.temperature_c | number:'1.1-1' }} °C</span>
      </div>
    </div>
  } @else {
    <p class="connecting">Connecting to sensor stream...</p>
  }

  <!-- Chat toggle -->
  <button class="chat-toggle" (click)="toggleChat()">
    {{ showChat ? 'Close AI Chat' : 'Ask AI Analyst' }}
  </button>

  @if (showChat) {
    <app-chat-panel />
  }
</div>
```

- [ ] **Step 7: Commit**

```bash
git add fractureguard-dashboard/src/app/services/telemetry.service.ts \
        fractureguard-dashboard/src/app/services/telemetry.service.spec.ts \
        fractureguard-dashboard/src/app/features/monitoring/
git commit -m "feat(dashboard): TelemetryService with signals and MonitoringComponent"
```

---

### Task 13: ChatService + ChatPanel

**Files:**
- Create: `src/app/services/chat.service.ts`
- Create: `src/app/features/chat/chat-panel.component.ts`
- Create: `src/app/features/chat/chat-panel.component.html`
- Test: `src/app/services/chat.service.spec.ts`

- [ ] **Step 1: Write failing test**

```typescript
// src/app/services/chat.service.spec.ts
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
```

- [ ] **Step 2: Run — expect FAIL**

```bash
ng test --include="**/chat.service.spec.ts" --watch=false
```

Expected: Compile error — `ChatService` not found.

- [ ] **Step 3: Create `src/app/services/chat.service.ts`**

```typescript
import { Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { ChatMessage } from '../models/chat.model';

const DEV_JWT = 'dev-engineer-token';  // replaced by MSAL token acquisition in prod

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
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
ng test --include="**/chat.service.spec.ts" --watch=false
```

Expected: All 3 tests PASS.

- [ ] **Step 5: Create `src/app/features/chat/chat-panel.component.ts`**

```typescript
import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatService } from '../../services/chat.service';

@Component({
  selector: 'app-chat-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-panel.component.html',
})
export class ChatPanelComponent {
  protected chat      = inject(ChatService);
  protected inputText = signal('');

  async send(): Promise<void> {
    const text = this.inputText().trim();
    if (!text) return;
    this.inputText.set('');
    await this.chat.sendMessage(text);
  }
}
```

- [ ] **Step 6: Create `src/app/features/chat/chat-panel.component.html`**

```html
<div class="chat-panel">
  <div class="messages">
    @for (msg of chat.messages(); track msg.timestamp) {
      <div class="message" [class.user]="msg.role === 'user'">
        <span class="role">{{ msg.role === 'user' ? 'You' : 'AI Analyst' }}</span>
        <p>{{ msg.content }}</p>
      </div>
    }
    @if (chat.isStreaming()) {
      <div class="message assistant">
        <span class="role">AI Analyst</span>
        <p class="cursor">▌</p>
      </div>
    }
  </div>

  <div class="input-row">
    <input
      [ngModel]="inputText()"
      (ngModelChange)="inputText.set($event)"
      (keydown.enter)="send()"
      placeholder="Ask about risk, sensor readings, protocols..."
      [disabled]="chat.isStreaming()"
    />
    <button (click)="send()" [disabled]="chat.isStreaming() || !inputText()">
      Send
    </button>
  </div>
</div>
```

- [ ] **Step 7: Commit**

```bash
git add fractureguard-dashboard/src/app/services/chat.service.ts \
        fractureguard-dashboard/src/app/services/chat.service.spec.ts \
        fractureguard-dashboard/src/app/features/chat/
git commit -m "feat(dashboard): ChatService with SSE streaming and ChatPanel"
```

---

### Task 14: AlertService + live alert banner wiring

**Files:**
- Create: `src/app/services/alert.service.ts`
- Create: `src/app/features/reports/reports.component.ts`
- Test: `src/app/services/alert.service.spec.ts`

- [ ] **Step 1: Write failing test**

```typescript
// src/app/services/alert.service.spec.ts
import { TestBed } from '@angular/core/testing';
import { AlertService } from './alert.service';

describe('AlertService', () => {
  let service: AlertService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
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
```

- [ ] **Step 2: Run — expect FAIL**

```bash
ng test --include="**/alert.service.spec.ts" --watch=false
```

Expected: Compile error — `AlertService` not found.

- [ ] **Step 3: Create `src/app/services/alert.service.ts`**

```typescript
import { Injectable, signal } from '@angular/core';
import { io } from 'socket.io-client';
import { environment } from '../../environments/environment';
import { AlertReport } from '../models/chat.model';

@Injectable({ providedIn: 'root' })
export class AlertService {
  readonly latestAlert = signal<AlertReport | null>(null);

  constructor() {
    const socket = io(environment.notifierUrl, { transports: ['websocket'] });
    socket.on('alert:report', (report: AlertReport) => {
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
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
ng test --include="**/alert.service.spec.ts" --watch=false
```

Expected: All 3 tests PASS.

- [ ] **Step 5: Create `src/app/features/reports/reports.component.ts`**

```typescript
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="reports-page">
      <h2>Historical Risk Reports</h2>
      <p class="subtitle">Reports generated by the AI analyst are stored here.</p>
    </div>
  `,
})
export class ReportsComponent {}
```

- [ ] **Step 6: Build to verify all components compile**

```bash
npm run build
```

Expected: 0 errors.

- [ ] **Step 7: Run all unit tests**

```bash
ng test --watch=false
```

Expected: All tests PASS.

- [ ] **Step 8: Commit**

```bash
git add fractureguard-dashboard/src/app/services/alert.service.ts \
        fractureguard-dashboard/src/app/services/alert.service.spec.ts \
        fractureguard-dashboard/src/app/features/reports/
git commit -m "feat(dashboard): AlertService, live alert banner, and Reports stub"
```

---

*Phase 4 complete → Phase 5 (Integration) requires this phase*
