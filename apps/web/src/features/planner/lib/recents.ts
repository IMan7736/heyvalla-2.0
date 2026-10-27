import type { Place } from "./types";

const KEY = "dhathuru.planner.recents";
const MAX = 5;

type Recent = Pick<Place, "id" | "type" | "name" | "geo"> & { code?: string };

function storage(): Storage | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}

export function readRecents(): Recent[] {
  try {
    const raw = storage()?.getItem(KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(
      (r): r is Recent =>
        typeof r === "object" &&
        r !== null &&
        typeof (r as Recent).id === "string" &&
        typeof (r as Recent).name?.en === "string",
    );
  } catch {
    return [];
  }
}

export function pushRecent(place: Recent): void {
  try {
    const next = [place, ...readRecents().filter((r) => r.id !== place.id)].slice(0, MAX);
    storage()?.setItem(KEY, JSON.stringify(next));
  } catch {
    /* storage unavailable (private mode): recents are a convenience only */
  }
}
