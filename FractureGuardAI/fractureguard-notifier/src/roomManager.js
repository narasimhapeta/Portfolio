const rooms = new Map();

export function joinRoom(sessionId, socketId) {
  if (!rooms.has(sessionId)) rooms.set(sessionId, new Set());
  rooms.get(sessionId).add(socketId);
}

export function leaveRoom(sessionId, socketId) {
  const set = rooms.get(sessionId);
  if (!set) return;
  set.delete(socketId);
  if (set.size === 0) rooms.delete(sessionId);
}

export function getRoomName(sessionId) {
  return `session:${sessionId}`;
}
