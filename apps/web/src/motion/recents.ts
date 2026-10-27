import { z } from "zod";

const STORAGE_KEY = "dhathuru:recents:v1";
export const MAX_RECENTS = 6;

const recentSchema = z.object({
  kind: z.enum(["place", "search", "trip"]),
  /** Place id, trip id, or a href for searches. */
  id: z.string().min(1),
  label: z.string().min(1),
  href: z.string().startsWith("/"),
  /** Epoch ms, UTC. */
  at: z.number().int().nonnegative(),
});
export type Recent = z.infer<typeof recentSchema>;

const listSchema = z.array(recentSchema);

type StorageLike = Pick<Storage, "getItem" | "setItem">;

function defaultStorage(): StorageLike | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}

/** Reads recents; corrupt or missing data yields an empty list rather than an error. */
export function readRecents(storage: StorageLike | null = defaultStorage()): Recent[] {
  if (!storage) return [];
  try {
    const raw = storage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = listSchema.safeParse(JSON.parse(raw));
    return parsed.success ? parsed.data : [];
  } catch {
    return [];
  }
}

/** Newest first, de-duplicated by kind+id, capped. Pure so it is easy to test. */
export function addRecent(
  list: readonly Recent[],
  entry: Recent,
  max: number = MAX_RECENTS,
): Recent[] {
  const rest = list.filter((r) => !(r.kind === entry.kind && r.id === entry.id));
  return [entry, ...rest].slice(0, max);
}

export function recordRecent(
  entry: Omit<Recent, "at">,
  now: number = Date.now(),
  storage: StorageLike | null = defaultStorage(),
): Recent[] {
  const next = addRecent(readRecents(storage), { ...entry, at: now });
  try {
    storage?.setItem(STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Storage full or blocked: recents are a convenience, never an error.
  }
  return next;
}
