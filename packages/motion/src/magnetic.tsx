"use client";

import { motion, useMotionValue, useSpring } from "motion/react";
import type { PointerEvent, ReactNode } from "react";
import { spring } from "./tokens";
import { usePrefersReducedMotion } from "./use-reduced-motion";

export type MagneticProps = {
  children: ReactNode;
  className?: string;
  /** Fraction of the pointer offset the element follows (0.15 is subtle). */
  strength?: number;
  /** Hard cap in px so large targets never wander. */
  maxOffset?: number;
};

export function clampOffset(delta: number, strength: number, maxOffset: number): number {
  const raw = delta * strength;
  return Math.max(-maxOffset, Math.min(maxOffset, raw));
}

/** Gently leans toward the pointer. Decorative only: layout and hit area do not change. */
export function Magnetic({ children, className, strength = 0.15, maxOffset = 8 }: MagneticProps) {
  const reduced = usePrefersReducedMotion();
  const x = useMotionValue(0);
  const y = useMotionValue(0);
  const sx = useSpring(x, spring.drift);
  const sy = useSpring(y, spring.drift);

  if (reduced) return <span className={className}>{children}</span>;

  const onMove = (event: PointerEvent<HTMLSpanElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    x.set(clampOffset(event.clientX - (rect.left + rect.width / 2), strength, maxOffset));
    y.set(clampOffset(event.clientY - (rect.top + rect.height / 2), strength, maxOffset));
  };
  const onLeave = () => {
    x.set(0);
    y.set(0);
  };

  return (
    <motion.span
      className={className}
      style={{ x: sx, y: sy, display: "inline-block" }}
      onPointerMove={onMove}
      onPointerLeave={onLeave}
    >
      {children}
    </motion.span>
  );
}
