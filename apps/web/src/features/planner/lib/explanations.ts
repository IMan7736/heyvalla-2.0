/**
 * Maps engine explanation / warning codes to message keys in messages/en/planner.json.
 * Unknown codes degrade gracefully (generic message) and are reported once through `onUnknown`.
 */
import type { Explanation, Warning } from "./types";

export type Severity = Explanation["severity"];

const EXPLANATION_KEYS: Record<string, { key: string; importance: number }> = {
  OVERNIGHT_REQUIRED: { key: "OVERNIGHT_REQUIRED", importance: 100 },
  BUFFER_TIGHT: { key: "BUFFER_TIGHT", importance: 90 },
  DELAY_HISTORY_POOR: { key: "DELAY_HISTORY_POOR", importance: 80 },
  LAST_SERVICE_OF_DAY: { key: "LAST_SERVICE_OF_DAY", importance: 70 },
  SAFEST_BUFFER: { key: "SAFEST_BUFFER", importance: 60 },
  BUFFER_COMFORTABLE: { key: "BUFFER_COMFORTABLE", importance: 55 },
  CHECKIN_CUTOFF_OK: { key: "CHECKIN_CUTOFF_OK", importance: 50 },
  DELAY_HISTORY_GOOD: { key: "DELAY_HISTORY_GOOD", importance: 45 },
  FASTEST_OVERALL: { key: "FASTEST_OVERALL", importance: 40 },
  CHEAPEST_OVERALL: { key: "CHEAPEST_OVERALL", importance: 40 },
  FEWER_TRANSFERS: { key: "FEWER_TRANSFERS", importance: 30 },
  ESTIMATED_TAXI_FARE: { key: "ESTIMATED_TAXI_FARE", importance: 20 },
};

const SEVERITY_RANK: Record<Severity, number> = { caution: 2, good: 1, info: 0 };

export const UNKNOWN_CODE_KEY = "UNKNOWN";

const reported = new Set<string>();

export function isKnownCode(code: string): boolean {
  return code in EXPLANATION_KEYS;
}

export type ResolvedMessage = {
  key: string;
  params: Record<string, string | number>;
  code: string;
  known: boolean;
};

function toParams(raw: Record<string, unknown>): Record<string, string | number> {
  const out: Record<string, string | number> = {};
  for (const [name, value] of Object.entries(raw)) {
    if (typeof value === "string" || typeof value === "number") out[name] = value;
  }
  return out;
}

export function resolveCode(
  code: string,
  params: Record<string, unknown>,
  onUnknown: (code: string) => void = logUnknownCode,
): ResolvedMessage {
  const entry = EXPLANATION_KEYS[code];
  if (!entry) {
    onUnknown(code);
    return { key: UNKNOWN_CODE_KEY, params: {}, code, known: false };
  }
  return { key: entry.key, params: toParams(params), code, known: true };
}

export function logUnknownCode(code: string): void {
  if (reported.has(code)) return;
  reported.add(code);
  console.warn(`[planner] unknown explanation code "${code}"`);
}

/** Most important first: cautions, then by catalogue importance. */
export function sortExplanations(explanations: readonly Explanation[]): Explanation[] {
  const weight = (e: Explanation) =>
    SEVERITY_RANK[e.severity] * 1000 + (EXPLANATION_KEYS[e.code]?.importance ?? 0);
  return [...explanations].sort((a, b) => weight(b) - weight(a));
}

/** The single line shown on a result card. */
export function headlineExplanation(explanations: readonly Explanation[]): Explanation | undefined {
  return sortExplanations(explanations).find((e) => isKnownCode(e.code));
}

export function warningMessage(
  warning: Warning,
  onUnknown?: (code: string) => void,
): ResolvedMessage {
  return resolveCode(warning.code, warning.params, onUnknown);
}
