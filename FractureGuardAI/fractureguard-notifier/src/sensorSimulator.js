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
