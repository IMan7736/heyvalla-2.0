import { describe, expect, it } from "vitest";
import { readDeviceSignals, shouldAnimateAtmosphere, waveY } from "./atmosphere-policy";

const capable = { reducedMotion: false, saveData: false, hardwareConcurrency: 8, deviceMemory: 8 };

describe("shouldAnimateAtmosphere", () => {
  it("runs on capable devices", () => {
    expect(shouldAnimateAtmosphere(capable)).toBe(true);
  });

  it("falls back for reduced motion, save-data, few cores and low memory", () => {
    expect(shouldAnimateAtmosphere({ ...capable, reducedMotion: true })).toBe(false);
    expect(shouldAnimateAtmosphere({ ...capable, saveData: true })).toBe(false);
    expect(shouldAnimateAtmosphere({ ...capable, hardwareConcurrency: 2 })).toBe(false);
    expect(shouldAnimateAtmosphere({ ...capable, deviceMemory: 2 })).toBe(false);
  });

  it("treats unknown hardware hints as capable (Firefox, Safari)", () => {
    expect(
      shouldAnimateAtmosphere({
        ...capable,
        hardwareConcurrency: undefined,
        deviceMemory: undefined,
      }),
    ).toBe(true);
  });
});

describe("readDeviceSignals", () => {
  it("reads navigator hints", () => {
    const nav = { hardwareConcurrency: 6, deviceMemory: 8, connection: { saveData: true } };
    expect(readDeviceSignals(false, nav as unknown as Navigator)).toEqual({
      reducedMotion: false,
      saveData: true,
      hardwareConcurrency: 6,
      deviceMemory: 8,
    });
  });
});

describe("waveY", () => {
  it("stays inside the canvas for every layer and time", () => {
    for (let layer = 0; layer < 4; layer++) {
      for (let t = 0; t < 60; t += 3.7) {
        for (let x = 0; x <= 1; x += 0.1) {
          const y = waveY(x, t, layer);
          expect(y).toBeGreaterThan(0);
          expect(y).toBeLessThan(1);
        }
      }
    }
  });
});
