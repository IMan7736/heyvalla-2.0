"use client";

import { useScroll, useTransform, type MotionValue } from "motion/react";
import { useRef, type RefObject } from "react";
import { usePrefersReducedMotion } from "./use-reduced-motion";

/** Maps scroll progress through an element (0 at entry, 1 at exit) onto an output range. */
export function useScrollMap<T extends HTMLElement>(
  output: [number, number],
): { ref: RefObject<T | null>; value: MotionValue<number>; reduced: boolean } {
  const reduced = usePrefersReducedMotion();
  const ref = useRef<T>(null);
  const { scrollYProgress } = useScroll({ target: ref, offset: ["start end", "end start"] });
  const value = useTransform(scrollYProgress, [0, 1], reduced ? [output[1], output[1]] : output);
  return { ref, value, reduced };
}

/** Page-level scroll progress 0..1 (reading bars, sticky header polish). */
export function usePageProgress(): MotionValue<number> {
  return useScroll().scrollYProgress;
}
