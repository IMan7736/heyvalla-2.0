"use client";

import { motion } from "motion/react";
import type { ComponentPropsWithoutRef, ReactNode } from "react";
import { distance, duration, ease } from "./tokens";
import { usePrefersReducedMotion } from "./use-reduced-motion";

export type RevealProps = Omit<
  ComponentPropsWithoutRef<"div">,
  "onDrag" | "onDragStart" | "onDragEnd" | "onAnimationStart" | "onAnimationEnd"
> & {
  children: ReactNode;
  /** Seconds before the reveal starts. */
  delay?: number;
  /** Offset in px the element rises from. */
  offset?: number;
  /** Wait until scrolled into view (once) instead of animating on mount. */
  inView?: boolean;
};

/** Fade and rise into place. Reduced motion renders the final state immediately. */
export function Reveal({
  children,
  delay = 0,
  offset = distance.rise,
  inView = false,
  ...rest
}: RevealProps) {
  const reduced = usePrefersReducedMotion();
  if (reduced) return <div {...rest}>{children}</div>;

  const hidden = { opacity: 0, y: offset };
  const shown = { opacity: 1, y: 0 };
  const transition = { duration: duration.base, ease: ease.tide, delay };
  return (
    <motion.div
      {...rest}
      initial={hidden}
      {...(inView
        ? { whileInView: shown, viewport: { once: true, margin: "0px 0px -10% 0px" } }
        : { animate: shown })}
      transition={transition}
    >
      {children}
    </motion.div>
  );
}
