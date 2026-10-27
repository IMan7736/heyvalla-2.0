"use client";

import { motion, type HTMLMotionProps } from "motion/react";
import { scale, spring } from "./tokens";
import { usePrefersReducedMotion } from "./use-reduced-motion";

export type PressableProps = HTMLMotionProps<"button">;

/**
 * Button with press and hover physics (scale only). Disabled buttons do not react.
 * Use for custom buttons; LAGOON's Button may compose it.
 */
export function Pressable({ children, disabled, whileHover, whileTap, ...rest }: PressableProps) {
  const reduced = usePrefersReducedMotion();
  const live = !reduced && !disabled;
  return (
    <motion.button
      {...rest}
      disabled={disabled}
      whileHover={live ? (whileHover ?? { scale: scale.hover }) : undefined}
      whileTap={live ? (whileTap ?? { scale: scale.press }) : undefined}
      transition={spring.snappy}
    >
      {children}
    </motion.button>
  );
}
