import type { ResultsState } from "./search-state";
import type { RiskLevel, TripOption } from "./types";

const RISK_RANK: Record<RiskLevel, number> = { low: 0, medium: 1, high: 2 };

type SortKey = ResultsState["sort"];

const comparators: Record<SortKey, (a: TripOption, b: TripOption) => number> = {
  best: (a, b) => b.score - a.score,
  fastest: (a, b) => a.durationMin - b.durationMin || b.score - a.score,
  cheapest: (a, b) => a.price.total.amountMinor - b.price.total.amountMinor || b.score - a.score,
  safest: (a, b) =>
    RISK_RANK[a.risk.level] - RISK_RANK[b.risk.level] ||
    a.risk.missProbability - b.risk.missProbability ||
    b.score - a.score,
  earliest: (a, b) => Date.parse(a.arriveAt) - Date.parse(b.arriveAt) || b.score - a.score,
};

export function sortOptions(options: readonly TripOption[], sort: SortKey): TripOption[] {
  return [...options].sort(comparators[sort]);
}

export function filterOptions(
  options: readonly TripOption[],
  filters: { maxPriceMinor: number | null; lowRiskOnly: boolean },
): TripOption[] {
  return options.filter(
    (o) =>
      (filters.maxPriceMinor === null || o.price.total.amountMinor <= filters.maxPriceMinor) &&
      (!filters.lowRiskOnly || o.risk.level === "low"),
  );
}

/** Smallest buffer relative to what the engine considers comfortable (0..1+). */
export function weakestBuffer(option: TripOption) {
  let weakest: TripOption["buffers"][number] | undefined;
  for (const b of option.buffers) {
    if (
      !weakest ||
      b.minutes / Math.max(1, b.comfortMinutes) <
        weakest.minutes / Math.max(1, weakest.comfortMinutes)
    ) {
      weakest = b;
    }
  }
  return weakest;
}

export type BufferTone = "comfortable" | "ok" | "tight";

export function bufferTone(b: {
  minutes: number;
  requiredMinutes: number;
  comfortMinutes: number;
}): BufferTone {
  if (b.minutes >= b.comfortMinutes) return "comfortable";
  if (b.minutes <= b.requiredMinutes + 5) return "tight";
  return "ok";
}

/** Fill ratio for the buffer meter: required = 0, comfort = 1 (clamped 0..1). */
export function bufferFill(b: {
  minutes: number;
  requiredMinutes: number;
  comfortMinutes: number;
}): number {
  const span = Math.max(1, b.comfortMinutes - b.requiredMinutes);
  return Math.min(1, Math.max(0, (b.minutes - b.requiredMinutes) / span));
}
