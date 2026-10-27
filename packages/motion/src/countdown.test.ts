import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { computeCountdown, formatCountdown, useCountdown } from "./countdown";

describe("formatCountdown", () => {
  it("formats m:ss and clamps negatives", () => {
    expect(formatCountdown(725)).toBe("12:05");
    expect(formatCountdown(9)).toBe("0:09");
    expect(formatCountdown(-4)).toBe("0:00");
  });
});

describe("computeCountdown", () => {
  it("marks urgent only in the final stretch and expired at zero", () => {
    expect(computeCountdown(100_000, 0, 60)).toMatchObject({ secondsLeft: 100, urgent: false });
    expect(computeCountdown(100_000, 50_000, 60)).toMatchObject({ secondsLeft: 50, urgent: true });
    expect(computeCountdown(100_000, 100_000, 60)).toMatchObject({ expired: true, urgent: false });
    expect(computeCountdown(100_000, 200_000, 60).label).toBe("0:00");
  });
});

describe("useCountdown", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-10-27T10:00:00Z"));
  });
  afterEach(() => vi.useRealTimers());

  it("ticks down each second and stops at expiry", () => {
    const expiresAt = Date.parse("2026-10-27T10:00:03Z");
    const { result } = renderHook(() => useCountdown(expiresAt, 2));
    expect(result.current.secondsLeft).toBe(3);
    act(() => void vi.advanceTimersByTime(1000));
    expect(result.current.secondsLeft).toBe(2);
    expect(result.current.urgent).toBe(true);
    act(() => void vi.advanceTimersByTime(5000));
    expect(result.current.expired).toBe(true);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("accepts an ISO string", () => {
    const { result } = renderHook(() => useCountdown("2026-10-27T10:12:00Z"));
    expect(result.current.label).toBe("12:00");
  });
});
