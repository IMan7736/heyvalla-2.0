import type { Money } from "./types";

export type SuggestedOption = {
  optionId: string;
  departAt: string;
  arriveAt: string;
  total: Money;
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** Reads the replan suggestion attached to a disruption; anything malformed is ignored rather than trusted. */
export function parseSuggestion(raw: unknown): SuggestedOption[] {
  if (!isRecord(raw) || !Array.isArray(raw.options)) return [];
  const out: SuggestedOption[] = [];
  for (const item of raw.options) {
    if (!isRecord(item) || !isRecord(item.total)) continue;
    const { optionId, departAt, arriveAt } = item;
    const { amountMinor, currency } = item.total;
    if (
      typeof optionId === "string" &&
      typeof departAt === "string" &&
      typeof arriveAt === "string" &&
      typeof amountMinor === "number" &&
      currency === "MVR"
    ) {
      out.push({ optionId, departAt, arriveAt, total: { amountMinor, currency } });
    }
  }
  return out;
}
