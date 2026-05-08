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
