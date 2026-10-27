import { describe, expect, it } from "vitest";
import { formatDay, formatDuration, formatMoney, formatTime } from "./format";

describe("format", () => {
  it("formats money from integer minor units", () => {
    expect(formatMoney({ amountMinor: 45000, currency: "MVR" })).toBe("MVR 450");
    expect(formatMoney({ amountMinor: 12550, currency: "MVR" })).toBe("MVR 125.50");
  });

  it("shows Maldives time (UTC+5) in 24-hour form", () => {
    expect(formatTime("2026-10-27T11:40:00Z")).toBe("16:40");
    expect(formatTime("2026-10-27T19:30:00Z")).toBe("00:30");
  });

  it("rolls the day over in Maldives time", () => {
    expect(formatDay("2026-10-27T20:00:00Z")).toContain("28");
  });

  it("formats durations with units", () => {
    expect(formatDuration(45)).toMatch(/45/);
    expect(formatDuration(125)).toMatch(/2.*5/);
  });
});
