import { formatDay, formatMoney, formatTime } from "@dhathuru/ui";

/**
 * Display helpers. Money, time and day formatting come from @dhathuru/ui so the planner and the
 * design system always agree (Maldives time, 24 h, day-first English). Pure date arithmetic lives here.
 */
export { formatMoney, formatTime };
export const formatDate = formatDay;

export const DISPLAY_TIME_ZONE = "Indian/Maldives";
const MALDIVES_OFFSET_MS = 5 * 60 * 60 * 1000;

/** Local Maldives calendar day (`YYYY-MM-DD`) of an instant. */
export function maldivesDay(iso: string): string {
  return new Date(new Date(iso).getTime() + MALDIVES_OFFSET_MS).toISOString().slice(0, 10);
}

/** Today's Maldives date (`YYYY-MM-DD`). */
export function maldivesToday(now: Date = new Date()): string {
  return new Date(now.getTime() + MALDIVES_OFFSET_MS).toISOString().slice(0, 10);
}

/** Shift a `YYYY-MM-DD` day by whole days. */
export function addDays(day: string, delta: number): string {
  const d = new Date(`${day}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + delta);
  return d.toISOString().slice(0, 10);
}

export function splitDuration(totalMinutes: number): { hours: number; minutes: number } {
  const safe = Math.max(0, Math.round(totalMinutes));
  return { hours: Math.floor(safe / 60), minutes: safe % 60 };
}

export function majorToMinor(major: number): number {
  return Math.round(major * 100);
}

export function minorToMajor(minor: number): number {
  return Math.trunc(minor / 100);
}

export function formatPercent(probability: number, locale = "en"): string {
  return new Intl.NumberFormat(locale, { style: "percent", maximumFractionDigits: 0 }).format(
    probability,
  );
}
