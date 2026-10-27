/**
 * Dhathuru motion tokens. Water metaphor: things arrive on a rising tide (slow-in is avoided,
 * we ease OUT quickly and settle softly) and leave with a short ebb.
 * Durations are seconds (Motion's unit). Keep in sync with docs/design/MOTION.md.
 */

export type CubicBezier = readonly [number, number, number, number];

export const duration = {
  /** Press feedback, colour shifts. */
  instant: 0.12,
  /** Hover, focus, small toggles. */
  quick: 0.2,
  /** Default UI change: reveals, popovers, swaps. */
  base: 0.35,
  /** Larger surfaces: panels, page transitions. */
  slow: 0.55,
  /** Hero atmosphere, route drawing. */
  tide: 0.9,
} as const;

export const ease = {
  /** Default. Quick arrival, soft settle, like water filling a basin. */
  tide: [0.22, 0.8, 0.28, 1] as CubicBezier,
  /** Elements leaving: gentle start, brisk finish (the ebb). */
  ebb: [0.5, 0, 0.75, 0.3] as CubicBezier,
  /** Symmetric moves within the screen (sliding pill, reorder). */
  swell: [0.45, 0.05, 0.25, 1] as CubicBezier,
  /** Constant-speed things only (progress, shimmer). */
  linear: [0, 0, 1, 1] as CubicBezier,
} as const;

export const spring = {
  /** Controls: pills, toggles, buttons. Crisp, no visible overshoot. */
  snappy: { type: "spring", stiffness: 520, damping: 40, mass: 0.8 },
  /** Cards, panels, shared elements. A single soft settle. */
  soft: { type: "spring", stiffness: 260, damping: 30, mass: 1 },
  /** Hover physics for magnetic and parallax: slow, floaty. */
  drift: { type: "spring", stiffness: 120, damping: 20, mass: 1.2 },
} as const;

export const stagger = {
  /** Seconds between siblings. */
  step: 0.05,
  /** Never stagger more than this many items, the rest enter together. */
  maxItems: 8,
} as const;

/** Distances in px for entrance offsets. Small on purpose: motion should hint, not shout. */
export const distance = {
  nudge: 6,
  rise: 12,
  slide: 24,
} as const;

export const scale = {
  press: 0.97,
  hover: 1.015,
  lift: 1.02,
} as const;

/** Total time a user can be blocked behind an animation. Nothing may exceed this. */
export const MAX_BLOCKING_SECONDS = 0.6;

export type DurationToken = keyof typeof duration;
export type EaseToken = keyof typeof ease;
export type SpringToken = keyof typeof spring;

/** Delay for the n-th sibling, capped so long lists do not feel slow. */
export function staggerDelay(index: number, step: number = stagger.step): number {
  const capped = Math.min(Math.max(index, 0), stagger.maxItems - 1);
  return capped * step;
}
