import {
  parseAsArrayOf,
  parseAsInteger,
  parseAsString,
  parseAsStringLiteral,
  type inferParserType,
} from "nuqs/server";
import { addDays, majorToMinor, maldivesToday } from "./format";
import { TRANSPORT_MODES, type TransportMode, type TripSearchRequest } from "./types";

export const TIME_MODES = ["departAfter", "arriveBy"] as const;
export const RISK_TOLERANCES = ["cautious", "balanced", "relaxed"] as const;
export const SORT_KEYS = ["best", "fastest", "cheapest", "safest", "earliest"] as const;

const PLACE_ID = /^(isl|atl|apt|jty|txs|poi|adr)_[0-9A-HJKMNP-TV-Z]{26}$/;
const DAY = /^\d{4}-\d{2}-\d{2}$/;
const TIME = /^([01]\d|2[0-3]):[0-5]\d$/;

/** Every search is the URL: shareable, back/forward friendly. */
export const searchParsers = {
  from: parseAsString,
  to: parseAsString,
  date: parseAsString,
  time: parseAsString.withDefault("09:00"),
  mode: parseAsStringLiteral(TIME_MODES).withDefault("departAfter"),
  adults: parseAsInteger.withDefault(1),
  children: parseAsInteger.withDefault(0),
  infants: parseAsInteger.withDefault(0),
  transfers: parseAsInteger,
  budget: parseAsInteger,
  modes: parseAsArrayOf(parseAsStringLiteral(TRANSPORT_MODES)).withDefault([]),
  risk: parseAsStringLiteral(RISK_TOLERANCES).withDefault("balanced"),
};

export const resultsParsers = {
  ...searchParsers,
  sort: parseAsStringLiteral(SORT_KEYS).withDefault("best"),
  maxPrice: parseAsInteger,
  lowRiskOnly: parseAsStringLiteral(["1"] as const),
  compare: parseAsArrayOf(parseAsString).withDefault([]),
};

export type SearchState = inferParserType<typeof searchParsers>;
export type ResultsState = inferParserType<typeof resultsParsers>;

export type SearchIssue =
  "MISSING_FROM" | "MISSING_TO" | "SAME_PLACE" | "BAD_DATE" | "BAD_TIME" | "PAST_DATE";

export function validateSearch(state: SearchState, today: string = maldivesToday()): SearchIssue[] {
  const issues: SearchIssue[] = [];
  if (!state.from || !PLACE_ID.test(state.from)) issues.push("MISSING_FROM");
  if (!state.to || !PLACE_ID.test(state.to)) issues.push("MISSING_TO");
  if (state.from && state.to && state.from === state.to) issues.push("SAME_PLACE");
  if (!state.date || !DAY.test(state.date)) issues.push("BAD_DATE");
  else if (state.date < today) issues.push("PAST_DATE");
  if (!TIME.test(state.time)) issues.push("BAD_TIME");
  return issues;
}

export function toRequest(state: SearchState): TripSearchRequest {
  const issues = validateSearch(state, "0000-00-00");
  if (issues.length > 0) throw new Error(`Invalid search: ${issues.join(",")}`);
  const modes: TransportMode[] = state.modes;
  return {
    from: state.from as string,
    to: state.to as string,
    date: state.date as string,
    time: state.time,
    timeMode: state.mode,
    passengers: {
      adults: clamp(state.adults, 1, 9),
      children: clamp(state.children, 0, 9),
      infants: clamp(state.infants, 0, 9),
    },
    preferences: {
      riskTolerance: state.risk,
      ...(state.transfers !== null ? { maxTransfers: clamp(state.transfers, 0, 8) } : {}),
      ...(state.budget !== null
        ? {
            budgetMax: {
              amountMinor: majorToMinor(Math.max(0, state.budget)),
              currency: "MVR" as const,
            },
          }
        : {}),
      ...(modes.length > 0 ? { modes } : {}),
    },
  };
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

/** Stable, order-insensitive key for TanStack Query. */
export function searchKey(state: SearchState): readonly unknown[] {
  return [
    "planner",
    "search",
    state.from,
    state.to,
    state.date,
    state.time,
    state.mode,
    state.adults,
    state.children,
    state.infants,
    state.transfers,
    state.budget,
    [...state.modes].sort(),
    state.risk,
  ];
}

/** Query string for `/results` built from a search state (omits defaults and empties). */
export function toSearchParams(state: SearchState): URLSearchParams {
  const p = new URLSearchParams();
  if (state.from) p.set("from", state.from);
  if (state.to) p.set("to", state.to);
  if (state.date) p.set("date", state.date);
  p.set("time", state.time);
  p.set("mode", state.mode);
  p.set("adults", String(state.adults));
  if (state.children > 0) p.set("children", String(state.children));
  if (state.infants > 0) p.set("infants", String(state.infants));
  if (state.transfers !== null) p.set("transfers", String(state.transfers));
  if (state.budget !== null) p.set("budget", String(state.budget));
  if (state.modes.length > 0) p.set("modes", state.modes.join(","));
  if (state.risk !== "balanced") p.set("risk", state.risk);
  return p;
}

export function shiftSearchTime(
  state: SearchState,
  minutes: number,
): Pick<SearchState, "date" | "time"> {
  const base = state.date ?? maldivesToday();
  const [h = 0, m = 0] = state.time.split(":").map(Number);
  const total = h * 60 + m + minutes;
  const dayShift = Math.floor(total / 1440);
  const inDay = ((total % 1440) + 1440) % 1440;
  const hh = String(Math.floor(inDay / 60)).padStart(2, "0");
  const mm = String(inDay % 60).padStart(2, "0");
  return { date: addDays(base, dayShift), time: `${hh}:${mm}` };
}

export interface TravelGroup {
  adults: number;
  children: number;
  infants: number;
}

const toCount = (raw: string | undefined, min: number, fallback: number): number => {
  const n = Number.parseInt(raw ?? "", 10);
  return Number.isFinite(n) ? Math.min(9, Math.max(min, n)) : fallback;
};

/** The searched group, read from the query string the results page carries to the detail page. */
export function groupFromParams(params: Record<string, string | string[] | undefined>): TravelGroup {
  const one = (k: string) => (typeof params[k] === "string" ? (params[k] as string) : undefined);
  const adults = toCount(one("adults"), 1, 1);
  return {
    adults,
    children: toCount(one("children"), 0, 0),
    infants: Math.min(toCount(one("infants"), 0, 0), adults),
  };
}

/** `/book` link for an option; prices are quoted for this group, so it must travel with the link. */
export function bookHref(optionId: string, group: TravelGroup): string {
  const p = new URLSearchParams({
    option: optionId,
    adults: String(group.adults),
    children: String(group.children),
    infants: String(group.infants),
  });
  return `/book?${p.toString()}`;
}
