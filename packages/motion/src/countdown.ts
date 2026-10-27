"use client";

import { useEffect, useState } from "react";

export type Countdown = {
  /** Whole seconds left, never negative. */
  secondsLeft: number;
  /** "m:ss" */
  label: string;
  expired: boolean;
  /** True in the final stretch: drive CountdownPulse / urgency styling from this. */
  urgent: boolean;
};

export function formatCountdown(totalSeconds: number): string {
  const safe = Math.max(0, Math.floor(totalSeconds));
  const minutes = Math.floor(safe / 60);
  const seconds = safe % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

export function computeCountdown(
  expiresAtMs: number,
  nowMs: number,
  urgentBelowSeconds: number,
): Countdown {
  const secondsLeft = Math.max(0, Math.ceil((expiresAtMs - nowMs) / 1000));
  return {
    secondsLeft,
    label: formatCountdown(secondsLeft),
    expired: secondsLeft === 0,
    urgent: secondsLeft > 0 && secondsLeft <= urgentBelowSeconds,
  };
}

/** Ticks once a second until `expiresAt` (ISO string or epoch ms). Used for booking holds. */
export function useCountdown(expiresAt: string | number, urgentBelowSeconds = 60): Countdown {
  const target = typeof expiresAt === "number" ? expiresAt : Date.parse(expiresAt);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    setNow(Date.now());
    if (Date.now() >= target) return;
    const id = window.setInterval(() => {
      const current = Date.now();
      setNow(current);
      if (current >= target) window.clearInterval(id);
    }, 1000);
    return () => window.clearInterval(id);
  }, [target]);

  return computeCountdown(target, now, urgentBelowSeconds);
}
