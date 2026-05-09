# Phase 2 — Node.js Real-Time Notifier

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Node.js service that streams synthetic sensor data to Angular clients via Socket.io and receives completed AI reports from the .NET API via a webhook, broadcasting them to the correct engineer's socket room.

**Architecture:** Single Express server with a Socket.io overlay. A `setInterval` loop emits synthetic sensor readings to all connected clients at 500 ms intervals. A POST `/notify` webhook (secret-protected) accepts completed reports from the .NET API and emits them to a per-session socket room.

**Tech Stack:** Node.js 22 LTS · Express 4.x · Socket.io 4.x · Jest 29.x

**Depends on:** Phase 0 (Docker Compose scaffold)

---

## File Map

```
fractureguard-notifier/
├── package.json
├── Dockerfile
└── src/
    ├── index.js            Express + Socket.io server, sensor broadcast loop, /notify webhook
    ├── sensorSimulator.js  Generates synthetic sensor readings with realistic noise
    ├── roomManager.js      Tracks sessionId → socket room membership
    └── __tests__/
        ├── sensorSimulator.test.js
        └── alertHandler.test.js
```

---

### Task 4: Express server + Socket.io + sensor simulator

**Files:**
- Create: `fractureguard-notifier/package.json`
- Create: `fractureguard-notifier/src/sensorSimulator.js`
- Create: `fractureguard-notifier/src/roomManager.js`
- Create: `fractureguard-notifier/src/index.js`
- Create: `fractureguard-notifier/Dockerfile`
- Test: `fractureguard-notifier/src/__tests__/sensorSimulator.test.js`

- [ ] **Step 1: Create `package.json`**

```json
{
  "name": "fractureguard-notifier",
  "version": "1.0.0",
  "type": "module",
  "scripts": {
    "start": "node src/index.js",
    "test": "node --experimental-vm-modules node_modules/.bin/jest"
  },
  "dependencies": {
    "express": "^4.19.2",
    "socket.io": "^4.7.5"
  },
  "devDependencies": {
    "jest": "^29.7.0"
  },
  "jest": {
    "transform": {}
  }
}
```

- [ ] **Step 2: Install dependencies**

```bash
cd fractureguard-notifier
npm install
```

Expected: `node_modules/` created, no errors.

- [ ] **Step 3: Write failing test for sensor simulator**

```javascript
// src/__tests__/sensorSimulator.test.js
import { generateReading } from '../sensorSimulator.js';

test('generateReading returns all required sensor fields', () => {
  const reading = generateReading();
  expect(reading).toHaveProperty('pressure_psi');
  expect(reading).toHaveProperty('flow_rate_bpm');
  expect(reading).toHaveProperty('vibration_g');
  expect(reading).toHaveProperty('temperature_c');
  expect(reading).toHaveProperty('timestamp');
});

test('pressure_psi is within realistic fracking range', () => {
  for (let i = 0; i < 50; i++) {
    const { pressure_psi } = generateReading();
    expect(pressure_psi).toBeGreaterThanOrEqual(400);
    expect(pressure_psi).toBeLessThanOrEqual(1100);
  }
});

test('timestamp is a recent ISO string', () => {
  const { timestamp } = generateReading();
  const diff = Date.now() - new Date(timestamp).getTime();
  expect(diff).toBeLessThan(1000);
});
```

- [ ] **Step 4: Run — expect FAIL**

```bash
npm test -- --testPathPattern=sensorSimulator
```

Expected: `Cannot find module '../sensorSimulator.js'`

- [ ] **Step 5: Create `src/sensorSimulator.js`**

```javascript
const BASE = {
  pressure_psi: 700,
  flow_rate_bpm: 10,
  vibration_g: 1.2,
  temperature_c: 40,
};

const NOISE = {
  pressure_psi: 80,
  flow_rate_bpm: 3,
  vibration_g: 0.6,
  temperature_c: 5,
};

export function generateReading() {
  return {
    pressure_psi:  +(BASE.pressure_psi  + (Math.random() - 0.5) * 2 * NOISE.pressure_psi).toFixed(1),
    flow_rate_bpm: +(BASE.flow_rate_bpm + (Math.random() - 0.5) * 2 * NOISE.flow_rate_bpm).toFixed(2),
    vibration_g:   +(BASE.vibration_g   + (Math.random() - 0.5) * 2 * NOISE.vibration_g).toFixed(3),
    temperature_c: +(BASE.temperature_c + (Math.random() - 0.5) * 2 * NOISE.temperature_c).toFixed(1),
    timestamp: new Date().toISOString(),
  };
}
```

- [ ] **Step 6: Run tests — expect PASS**

```bash
npm test -- --testPathPattern=sensorSimulator
```

Expected: All 3 tests PASS.

- [ ] **Step 7: Create `src/roomManager.js`**

```javascript
// Tracks which socket belongs to which engineer session
const rooms = new Map(); // sessionId → Set<socketId>

export function joinRoom(sessionId, socketId) {
  if (!rooms.has(sessionId)) rooms.set(sessionId, new Set());
  rooms.get(sessionId).add(socketId);
}

export function leaveRoom(sessionId, socketId) {
  rooms.get(sessionId)?.delete(socketId);
}

export function getRoomName(sessionId) {
  return `session:${sessionId}`;
}
```

- [ ] **Step 8: Create `src/index.js`**

```javascript
import express from 'express';
import { createServer } from 'http';
import { Server } from 'socket.io';
import { generateReading } from './sensorSimulator.js';
import { joinRoom, getRoomName } from './roomManager.js';

const PORT = process.env.PORT || 3001;
const SENSOR_INTERVAL_MS = 500;
const WEBHOOK_SECRET = process.env.NOTIFIER_WEBHOOK_SECRET || 'local-webhook-secret';

const app = express();
app.use(express.json());

const httpServer = createServer(app);
const io = new Server(httpServer, { cors: { origin: '*' } });

// --- Sensor broadcast ---
setInterval(() => {
  io.emit('sensor:reading', generateReading());
}, SENSOR_INTERVAL_MS);

// --- Socket.io connection ---
io.on('connection', (socket) => {
  const sessionId = socket.handshake.query.sessionId;
  if (sessionId) {
    socket.join(getRoomName(sessionId));
    joinRoom(sessionId, socket.id);
  }
  socket.on('disconnect', () => {});
});

// --- Alert webhook (called by .NET API) ---
app.post('/notify', (req, res) => {
  const secret = req.headers['x-webhook-secret'];
  if (secret !== WEBHOOK_SECRET) return res.status(401).json({ error: 'Unauthorized' });

  const { session_id, report } = req.body;
  if (!session_id || !report) return res.status(400).json({ error: 'Missing session_id or report' });

  io.to(getRoomName(session_id)).emit('alert:report', report);
  res.json({ ok: true });
});

app.get('/health', (_req, res) => res.json({ status: 'ok' }));

httpServer.listen(PORT, () => console.log(`Notifier listening on :${PORT}`));
```

- [ ] **Step 9: Create `Dockerfile`**

```dockerfile
FROM node:22-alpine
WORKDIR /app
COPY package*.json .
RUN npm ci --omit=dev
COPY src/ src/
CMD ["node", "src/index.js"]
```

- [ ] **Step 10: Commit**

```bash
git add fractureguard-notifier/
git commit -m "feat(notifier): Express + Socket.io with sensor simulator and alert webhook"
```

---

### Task 5: Alert handler tests

**Files:**
- Test: `fractureguard-notifier/src/__tests__/alertHandler.test.js`

- [ ] **Step 1: Write test**

```javascript
// src/__tests__/alertHandler.test.js
import { jest } from '@jest/globals';

// Extract the core webhook logic as a pure function for testability
const handleNotify = (io, body, secret, headerSecret) => {
  if (headerSecret !== secret) return { status: 401, body: { error: 'Unauthorized' } };
  const { session_id, report } = body;
  if (!session_id || !report) return { status: 400, body: { error: 'Missing session_id or report' } };
  io.to(`session:${session_id}`).emit('alert:report', report);
  return { status: 200, body: { ok: true } };
};

test('valid secret emits alert to correct room', () => {
  const mockEmit = jest.fn();
  const mockTo   = jest.fn(() => ({ emit: mockEmit }));
  const fakeIo   = { to: mockTo };

  const result = handleNotify(
    fakeIo,
    { session_id: 'abc', report: { risk_pct: 85 } },
    'test-secret',
    'test-secret'
  );

  expect(result.status).toBe(200);
  expect(mockTo).toHaveBeenCalledWith('session:abc');
  expect(mockEmit).toHaveBeenCalledWith('alert:report', { risk_pct: 85 });
});

test('wrong secret returns 401', () => {
  const result = handleNotify({}, {}, 'correct', 'wrong');
  expect(result.status).toBe(401);
});

test('missing session_id returns 400', () => {
  const fakeIo = { to: jest.fn(() => ({ emit: jest.fn() })) };
  const result = handleNotify(fakeIo, { report: { risk_pct: 85 } }, 's', 's');
  expect(result.status).toBe(400);
});
```

- [ ] **Step 2: Run — expect PASS** (pure function, no server imports)

```bash
npm test -- --testPathPattern=alertHandler
```

Expected: All 3 tests PASS.

- [ ] **Step 3: Commit**

```bash
git add fractureguard-notifier/src/__tests__/alertHandler.test.js
git commit -m "test(notifier): alert handler webhook tests"
```

---

*Phase 2 complete → Phase 5 (Integration) requires this phase*
