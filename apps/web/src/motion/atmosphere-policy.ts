export type DeviceSignals = {
  reducedMotion: boolean;
  saveData: boolean;
  hardwareConcurrency: number | undefined;
  deviceMemory: number | undefined;
};

/** Below these the canvas is skipped and the static gradient stays. */
export const MIN_CORES = 4;
export const MIN_MEMORY_GB = 4;

/** Should the animated lagoon effect run on this device? */
export function shouldAnimateAtmosphere(signals: DeviceSignals): boolean {
  if (signals.reducedMotion || signals.saveData) return false;
  if (signals.hardwareConcurrency !== undefined && signals.hardwareConcurrency < MIN_CORES)
    return false;
  if (signals.deviceMemory !== undefined && signals.deviceMemory < MIN_MEMORY_GB) return false;
  return true;
}

type NavigatorWithHints = Navigator & {
  deviceMemory?: number;
  connection?: { saveData?: boolean };
};

export function readDeviceSignals(
  reducedMotion: boolean,
  nav: Navigator = navigator,
): DeviceSignals {
  const hinted = nav as NavigatorWithHints;
  return {
    reducedMotion,
    saveData: hinted.connection?.saveData === true,
    hardwareConcurrency: nav.hardwareConcurrency,
    deviceMemory: hinted.deviceMemory,
  };
}

/** y offset (0..1 of height) of wave layer `layer` at horizontal fraction x and time t seconds. */
export function waveY(x: number, t: number, layer: number): number {
  const base = 0.38 + layer * 0.14;
  const phase = t * (0.22 + layer * 0.07);
  return (
    base +
    0.035 * Math.sin(x * (5 + layer * 1.7) * Math.PI + phase) +
    0.02 * Math.sin(x * (11 + layer * 2.3) * Math.PI - phase * 1.3)
  );
}
