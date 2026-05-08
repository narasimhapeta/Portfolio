const rooms = new Map();

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
