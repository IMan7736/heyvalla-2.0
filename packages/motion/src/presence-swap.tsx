"use client";

import { AnimatePresence, motion } from "motion/react";
import type { ReactNode } from "react";
import { distance, duration, ease } from "./tokens";
import { usePrefersReducedMotion } from "./use-reduced-motion";

export type PresenceSwapProps = {
  /** Changing this key swaps the content. */
  swapKey: string | number;
  children: ReactNode;
  className?: string;
  /** Vertical travel direction of the incoming content. */
  direction?: "up" | "down";
};

/** Cross-fades between states (loading -> result, step -> step). Out first, then in. */
export function PresenceSwap({
  swapKey,
  children,
  className,
  direction = "up",
}: PresenceSwapProps) {
  const reduced = usePrefersReducedMotion();
  if (reduced) {
    return (
      <div key={swapKey} className={className}>
        {children}
      </div>
    );
  }
  const sign = direction === "up" ? 1 : -1;
  return (
    <AnimatePresence mode="wait" initial={false}>
      <motion.div
        key={swapKey}
        className={className}
        initial={{ opacity: 0, y: sign * distance.nudge }}
        animate={{ opacity: 1, y: 0, transition: { duration: duration.base, ease: ease.tide } }}
        exit={{
          opacity: 0,
          y: sign * -distance.nudge,
          transition: { duration: duration.quick, ease: ease.ebb },
        }}
      >
        {children}
      </motion.div>
    </AnimatePresence>
  );
}
