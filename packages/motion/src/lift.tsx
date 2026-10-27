"use client";

import { motion, type HTMLMotionProps } from "motion/react";
import { scale, spring } from "./tokens";
import { usePrefersReducedMotion } from "./use-reduced-motion";

export type LiftProps = HTMLMotionProps<"div">;

/** Card hover lift: tiny rise + scale (transform only). The shadow change belongs in CSS opacity. */
export function Lift({ children, ...rest }: LiftProps) {
  const reduced = usePrefersReducedMotion();
  return (
    <motion.div
      {...rest}
      whileHover={reduced ? undefined : { y: -3, scale: scale.hover }}
      transition={spring.soft}
    >
      {children}
    </motion.div>
  );
}
