/** Whole seconds left on a hold; never negative. */
export function secondsLeft(expiresAt: string | null | undefined, now: number): number {
  if (!expiresAt) return 0;
  return Math.max(0, Math.ceil((new Date(expiresAt).getTime() - now) / 1000));
}

export function formatCountdown(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`;
}

/** Below this many seconds the countdown turns urgent. */
export const URGENT_SECONDS = 120;
