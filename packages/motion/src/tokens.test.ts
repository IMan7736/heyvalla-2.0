import { describe, expect, it } from "vitest";
import { duration, ease, MAX_BLOCKING_SECONDS, stagger, staggerDelay } from "./tokens";

describe("tokens", () => {
  it("keeps every UI duration under the blocking budget except atmosphere-scale tide", () => {
    for (const [name, value] of Object.entries(duration)) {
      if (name === "tide") continue;
      expect(value).toBeLessThanOrEqual(MAX_BLOCKING_SECONDS);
    }
  });

  it("orders durations from fastest to slowest", () => {
    const values = Object.values(duration);
    expect([...values].sort((a, b) => a - b)).toEqual(values);
  });

  it("uses valid cubic-bezier control points", () => {
    for (const curve of Object.values(ease)) {
      expect(curve).toHaveLength(4);
      expect(curve[0]).toBeGreaterThanOrEqual(0);
      expect(curve[0]).toBeLessThanOrEqual(1);
      expect(curve[2]).toBeGreaterThanOrEqual(0);
      expect(curve[2]).toBeLessThanOrEqual(1);
    }
  });

  it("caps stagger delay so long lists do not feel slow", () => {
    expect(staggerDelay(0)).toBe(0);
    expect(staggerDelay(3)).toBeCloseTo(3 * stagger.step);
    expect(staggerDelay(50)).toBeCloseTo((stagger.maxItems - 1) * stagger.step);
    expect(staggerDelay(-2)).toBe(0);
  });
});
