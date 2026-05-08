import express from 'express';
import { createServer } from 'http';
import { Server } from 'socket.io';
import { timingSafeEqual } from 'crypto';
import { generateReading } from './sensorSimulator.js';
import { joinRoom, leaveRoom, getRoomName } from './roomManager.js';

const PORT = process.env.PORT || 3001;
const SENSOR_INTERVAL_MS = 500;
const WEBHOOK_SECRET = process.env.NOTIFIER_WEBHOOK_SECRET || 'local-webhook-secret';
const ALLOWED_ORIGIN = process.env.ALLOWED_ORIGIN || 'http://localhost:4200';

if (!process.env.NOTIFIER_WEBHOOK_SECRET) {
  console.warn('WARNING: using default webhook secret — set NOTIFIER_WEBHOOK_SECRET in production');
}

const app = express();
app.use(express.json());

const httpServer = createServer(app);
const io = new Server(httpServer, { cors: { origin: ALLOWED_ORIGIN } });

io.on('connection', (socket) => {
  const sessionId = socket.handshake.query.sessionId;
  if (sessionId) {
    socket.join(getRoomName(sessionId));
    joinRoom(sessionId, socket.id);
  }
  socket.on('disconnect', () => {
    if (sessionId) leaveRoom(sessionId, socket.id);
  });
});

function secretsMatch(a, b) {
  try {
    return timingSafeEqual(Buffer.from(a), Buffer.from(b));
  } catch {
    return false;
  }
}

app.post('/notify', (req, res) => {
  const secret = req.headers['x-webhook-secret'];
  if (!secret || !secretsMatch(secret, WEBHOOK_SECRET)) {
    return res.status(401).json({ error: 'Unauthorized' });
  }

  const { session_id, report } = req.body;
  if (!session_id || !report) {
    return res.status(400).json({ error: 'Missing session_id or report' });
  }
  if (!/^[a-zA-Z0-9_-]{1,128}$/.test(session_id)) {
    return res.status(400).json({ error: 'Invalid session_id' });
  }

  io.to(getRoomName(session_id)).emit('alert:report', report);
  res.json({ ok: true });
});

app.get('/health', (_req, res) => res.json({ status: 'ok' }));

httpServer.listen(PORT, () => {
  console.log(`Notifier listening on :${PORT}`);
  setInterval(() => io.emit('sensor:reading', generateReading()), SENSOR_INTERVAL_MS);
});
