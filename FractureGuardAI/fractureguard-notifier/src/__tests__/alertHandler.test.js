import { jest } from '@jest/globals';

const handleNotify = (io, body, secret, headerSecret) => {
  if (!headerSecret || headerSecret !== secret) return { status: 401, body: { error: 'Unauthorized' } };
  const { session_id, report } = body;
  if (!session_id || !report) return { status: 400, body: { error: 'Missing session_id or report' } };
  if (!/^[a-zA-Z0-9_-]{1,128}$/.test(session_id)) return { status: 400, body: { error: 'Invalid session_id' } };
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

test('missing report returns 400', () => {
  const fakeIo = { to: jest.fn(() => ({ emit: jest.fn() })) };
  const result = handleNotify(fakeIo, { session_id: 'abc' }, 's', 's');
  expect(result.status).toBe(400);
});

test('invalid session_id returns 400', () => {
  const fakeIo = { to: jest.fn(() => ({ emit: jest.fn() })) };
  const result = handleNotify(fakeIo, { session_id: '<script>', report: {} }, 's', 's');
  expect(result.status).toBe(400);
});
