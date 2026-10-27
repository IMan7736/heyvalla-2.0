"use client";

import { LayoutGroup, motion } from "motion/react";
import { useId } from "react";
import { spring } from "./tokens";
import { usePrefersReducedMotion } from "./use-reduced-motion";

export type SlidingPillProps = {
  /** Render only for the active item. Shared across items via the same `group`. */
  active: boolean;
  /** Items of one segmented control share this id so the pill travels between them. */
  group: string;
  className?: string;
};

/**
 * The sliding highlight behind the active segment / tab. Place it as the first child of
 * the active item (item needs position: relative) and wrap the set in <PillGroup>.
 */
export function SlidingPill({ active, group, className }: SlidingPillProps) {
  const reduced = usePrefersReducedMotion();
  if (!active) return null;
  return (
    <motion.span
      aria-hidden="true"
      layoutId={`pill-${group}`}
      className={className}
      transition={reduced ? { duration: 0 } : spring.snappy}
      style={{ position: "absolute", inset: 0, zIndex: -1 }}
    />
  );
}

/** Scopes the shared layout id so two segmented controls on one page never fight. */
export function PillGroup({ children, id }: { children: React.ReactNode; id?: string }) {
  const fallback = useId();
  return <LayoutGroup id={id ?? fallback}>{children}</LayoutGroup>;
}
