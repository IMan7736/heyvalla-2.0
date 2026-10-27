"use client";

import { useEffect, useState, type CSSProperties } from "react";
import { usePrefersReducedMotion } from "./use-reduced-motion";

/** One shared shimmer period so every skeleton on screen sweeps in step. */
export const SHIMMER_PERIOD_MS = 1600;

/** Negative animation delay that aligns a freshly mounted skeleton with the global clock. */
export function shimmerDelayMs(now: number, period: number = SHIMMER_PERIOD_MS): number {
  return -(now % period);
}

export type SkeletonProps = {
  className?: string;
  style?: CSSProperties;
};

/**
 * Placeholder block. The sweep is a CSS keyframe (`dhathuru-shimmer`, see styles.css) that
 * runs on transform only; the delay syncs it to the shared clock. Reduced motion: static tint.
 */
export function Skeleton({ className, style }: SkeletonProps) {
  const reduced = usePrefersReducedMotion();
  const [delay, setDelay] = useState(0);
  useEffect(() => setDelay(shimmerDelayMs(performance.timeOrigin + performance.now())), []);
  return (
    <span
      aria-hidden="true"
      className={["dhathuru-skeleton", className].filter(Boolean).join(" ")}
      data-reduced={reduced ? "true" : undefined}
      style={{ ["--shimmer-delay" as string]: `${delay}ms`, ...style }}
    />
  );
}
