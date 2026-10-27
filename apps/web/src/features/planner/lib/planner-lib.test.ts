import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it, vi } from "vitest";
import { makeOption } from "../test/fixtures";
import { headlineExplanation, isKnownCode, resolveCode, sortExplanations } from "./explanations";
import {
  addDays,
  formatMoney,
  formatTime,
  maldivesDay,
  maldivesToday,
  minorToMajor,
  splitDuration,
} from "./format";
import { bufferFill, bufferTone, filterOptions, sortOptions, weakestBuffer } from "./results";
import {
  bookHref,
  groupFromParams,
  shiftSearchTime,
  toRequest,
  toSearchParams,
  validateSearch,
  type SearchState,
} from "./search-state";

const FROM = "isl_01K8Z3M5Q7R9T2V4W6X8Y0A1B2";
const TO = "isl_01K8Z3M5Q7R9T2V4W6X8Y0A1F2";

const base: SearchState = {
  from: FROM,
  to: TO,
  date: "2026-10-27",
  time: "17:00",
  mode: "arriveBy",
  adults: 2,
  children: 0,
  infants: 0,
  transfers: null,
  budget: null,
  modes: [],
  risk: "balanced",
};

describe("search state", () => {
  it("builds the API request from URL state, integers for money", () => {
    const req = toRequest({
      ...base,
      budget: 2500,
      transfers: 2,
      modes: ["ferry"],
      risk: "cautious",
    });
    expect(req).toEqual({
      from: FROM,
      to: TO,
      date: "2026-10-27",
      time: "17:00",
      timeMode: "arriveBy",
      passengers: { adults: 2, children: 0, infants: 0 },
      preferences: {
        riskTolerance: "cautious",
        maxTransfers: 2,
        budgetMax: { amountMinor: 250000, currency: "MVR" },
        modes: ["ferry"],
      },
    });
  });

  it("omits unset preferences", () => {
    expect(toRequest(base).preferences).toEqual({ riskTolerance: "balanced" });
  });

  it("validates required fields, same place, bad date and past date", () => {
    expect(validateSearch({ ...base, from: null, to: null, date: null }, "2026-10-26")).toEqual([
      "MISSING_FROM",
      "MISSING_TO",
      "BAD_DATE",
    ]);
    expect(validateSearch({ ...base, to: FROM }, "2026-10-26")).toContain("SAME_PLACE");
    expect(validateSearch({ ...base, date: "2026-10-01" }, "2026-10-26")).toContain("PAST_DATE");
    expect(validateSearch({ ...base, time: "25:00" }, "2026-10-26")).toContain("BAD_TIME");
    expect(validateSearch(base, "2026-10-26")).toEqual([]);
  });

  it("throws rather than send an invalid request", () => {
    expect(() => toRequest({ ...base, from: null })).toThrow();
  });

  it("serialises to a shareable query and omits defaults", () => {
    const qs = toSearchParams(base).toString();
    expect(qs).toContain(`from=${FROM}`);
    expect(qs).toContain("mode=arriveBy");
    expect(qs).not.toContain("risk=");
    expect(qs).not.toContain("children=");
  });

  it("shifts time across midnight", () => {
    expect(shiftSearchTime({ ...base, time: "23:00" }, 120)).toEqual({
      date: "2026-10-28",
      time: "01:00",
    });
    expect(shiftSearchTime({ ...base, time: "01:00" }, -120)).toEqual({
      date: "2026-10-26",
      time: "23:00",
    });
  });
});

describe("format", () => {
  it("shows Indian/Maldives time (UTC+5)", () => {
    expect(formatTime("2026-10-27T02:30:00Z")).toBe("07:30");
    expect(maldivesDay("2026-10-26T20:00:00Z")).toBe("2026-10-27");
    expect(maldivesToday(new Date("2026-10-26T20:00:00Z"))).toBe("2026-10-27");
  });

  it("formats money from minor units without floats", () => {
    expect(formatMoney({ amountMinor: 207000, currency: "MVR" })).toBe("MVR 2,070");
    expect(formatMoney({ amountMinor: 9050, currency: "MVR" })).toBe("MVR 90.50");
    expect(minorToMajor(9050)).toBe(90);
  });

  it("handles durations and day arithmetic", () => {
    expect(splitDuration(545)).toEqual({ hours: 9, minutes: 5 });
    expect(addDays("2026-12-31", 1)).toBe("2027-01-01");
  });
});

describe("explanations", () => {
  it("resolves known codes with params", () => {
    expect(resolveCode("BUFFER_COMFORTABLE", { minutes: 45 })).toMatchObject({
      known: true,
      params: { minutes: 45 },
    });
  });

  it("degrades unknown codes gracefully and reports them", () => {
    const seen = vi.fn();
    const r = resolveCode("SOMETHING_NEW", { x: 1 }, seen);
    expect(r).toMatchObject({ known: false, key: "UNKNOWN" });
    expect(seen).toHaveBeenCalledWith("SOMETHING_NEW");
    expect(isKnownCode("SOMETHING_NEW")).toBe(false);
  });

  it("puts cautions first, then by importance", () => {
    const sorted = sortExplanations([
      { code: "FASTEST_OVERALL", params: {}, severity: "good" },
      { code: "BUFFER_TIGHT", params: { minutes: 8 }, severity: "caution" },
      { code: "ESTIMATED_TAXI_FARE", params: {}, severity: "info" },
    ]);
    expect(sorted.map((e) => e.code)).toEqual([
      "BUFFER_TIGHT",
      "FASTEST_OVERALL",
      "ESTIMATED_TAXI_FARE",
    ]);
    expect(headlineExplanation(sorted)?.code).toBe("BUFFER_TIGHT");
  });

  it("has an English message for every catalogue code in CONTRACT", () => {
    const messages = JSON.parse(
      readFileSync(path.join(process.cwd(), "messages/en/planner.json"), "utf8"),
    ) as { explain: Record<string, string> };
    const contractCodes = [
      "FASTEST_OVERALL",
      "CHEAPEST_OVERALL",
      "SAFEST_BUFFER",
      "BUFFER_COMFORTABLE",
      "BUFFER_TIGHT",
      "LAST_SERVICE_OF_DAY",
      "CHECKIN_CUTOFF_OK",
      "OVERNIGHT_REQUIRED",
      "FEWER_TRANSFERS",
      "ESTIMATED_TAXI_FARE",
      "DELAY_HISTORY_GOOD",
      "DELAY_HISTORY_POOR",
    ];
    for (const code of contractCodes) {
      expect(isKnownCode(code), code).toBe(true);
      expect(messages.explain[code], code).toBeTruthy();
    }
    expect(messages.explain.UNKNOWN).toBeTruthy();
  });
});

describe("results helpers", () => {
  const cheap = makeOption({
    id: "trp_A",
    price: { total: { amountMinor: 1000, currency: "MVR" }, breakdown: [] },
    durationMin: 600,
    score: 50,
    risk: { missProbability: 0.2, level: "high" },
  });
  const fast = makeOption({ id: "trp_B", durationMin: 200, score: 70 });
  const safe = makeOption({ id: "trp_C", score: 90 });

  it("sorts by each key", () => {
    const all = [cheap, fast, safe];
    expect(sortOptions(all, "cheapest")[0]?.id).toBe("trp_A");
    expect(sortOptions(all, "fastest")[0]?.id).toBe("trp_B");
    expect(sortOptions(all, "best")[0]?.id).toBe("trp_C");
    expect(sortOptions(all, "safest").at(-1)?.id).toBe("trp_A");
  });

  it("filters by price and risk and does not mutate", () => {
    const all = [cheap, fast, safe];
    expect(
      filterOptions(all, { maxPriceMinor: 5000, lowRiskOnly: false }).map((o) => o.id),
    ).toEqual(["trp_A"]);
    expect(filterOptions(all, { maxPriceMinor: null, lowRiskOnly: true }).map((o) => o.id)).toEqual(
      ["trp_B", "trp_C"],
    );
    expect(all).toHaveLength(3);
  });

  it("classifies buffers", () => {
    expect(bufferTone({ minutes: 120, requiredMinutes: 90, comfortMinutes: 120 })).toBe(
      "comfortable",
    );
    expect(bufferTone({ minutes: 93, requiredMinutes: 90, comfortMinutes: 120 })).toBe("tight");
    expect(bufferTone({ minutes: 105, requiredMinutes: 90, comfortMinutes: 120 })).toBe("ok");
    expect(bufferFill({ minutes: 90, requiredMinutes: 90, comfortMinutes: 120 })).toBe(0);
    expect(bufferFill({ minutes: 500, requiredMinutes: 90, comfortMinutes: 120 })).toBe(1);
    expect(weakestBuffer(makeOption({ buffers: [] }))).toBeUndefined();
  });
});

describe("book hand-off", () => {
  it("carries the searched group to /book", () => {
    const group = groupFromParams({ adults: "2", children: "1", infants: "1" });
    expect(bookHref("trp_1", group)).toBe("/book?option=trp_1&adults=2&children=1&infants=1");
  });
  it("clamps nonsense: at least one adult, infants never exceed adults", () => {
    expect(groupFromParams({ adults: "0", infants: "5" })).toEqual({ adults: 1, children: 0, infants: 1 });
    expect(groupFromParams({})).toEqual({ adults: 1, children: 0, infants: 0 });
  });
});
