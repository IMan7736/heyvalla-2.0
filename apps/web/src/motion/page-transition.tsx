"use client";

import { ViewTransition, type ReactNode } from "react";

/** Pass to <Link transitionTypes={[NAV_FORWARD]}> when going deeper (search -> results -> booking). */
export const NAV_FORWARD = "nav-forward";
/** Pass when returning up the hierarchy. */
export const NAV_BACK = "nav-back";

const classes = {
  [NAV_FORWARD]: "tide-forward",
  [NAV_BACK]: "tide-back",
  default: "tide-fade",
} as const;

/**
 * Route transition: uses React's <ViewTransition> (browser View Transitions API). Unsupported
 * browsers navigate instantly, which is the correct fallback; no JS animation library is involved,
 * so there is no flash and focus/scroll are untouched.
 *
 * Mount it in a `template.tsx` (re-created on every navigation), never in a layout.
 * Styles live in motion.css; reduced motion keeps a short crossfade and drops all travel.
 */
export function PageTransition({ children }: { children: ReactNode }) {
  return (
    <ViewTransition enter={classes} exit={classes} default="none">
      {children}
    </ViewTransition>
  );
}
