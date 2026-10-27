"use client";

import { motion } from "motion/react";
import { duration, ease } from "./tokens";
import { usePrefersReducedMotion } from "./use-reduced-motion";

export type DrawPathProps = {
  d: string;
  className?: string;
  stroke?: string;
  strokeWidth?: number | string;
  strokeLinecap?: "butt" | "round" | "square";
  strokeLinejoin?: "miter" | "round" | "bevel";
  strokeDasharray?: string;
  opacity?: number;
  /** Seconds. Defaults to the long "tide" duration used for route drawing. */
  drawDuration?: number;
  delay?: number;
  /** 0..1: how much of the path is drawn. Animates when it changes (e.g. a scrubber). */
  progress?: number;
};

/** SVG path that draws itself. Reduced motion shows the finished line. */
export function DrawPath({
  d,
  drawDuration = duration.tide,
  delay = 0,
  progress = 1,
  strokeLinecap = "round",
  ...rest
}: DrawPathProps) {
  const reduced = usePrefersReducedMotion();
  return (
    <motion.path
      {...rest}
      d={d}
      strokeLinecap={strokeLinecap}
      fill="none"
      initial={reduced ? false : { pathLength: 0 }}
      animate={{ pathLength: progress }}
      transition={reduced ? { duration: 0 } : { duration: drawDuration, ease: ease.swell, delay }}
    />
  );
}
