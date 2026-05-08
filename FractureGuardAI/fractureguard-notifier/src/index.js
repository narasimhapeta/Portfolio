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

setInterval(() => {
  io.emit('sensor:reading', generateReading());
}, SENSOR_INTERVAL_MS);

io.on('connection', (socket) => {
  const sessionId = socket.handshake.query.sessionId;
  if (sessionId) {
    socket.join(getRoomName(sessionId));
    joinRoom(sessionId, socket.id);
  }
  socket.on('disconnect', () => {});
});

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
