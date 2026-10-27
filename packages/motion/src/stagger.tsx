"use client";

import { motion, type Variants } from "motion/react";
import { Children, type ComponentPropsWithoutRef, type ReactNode } from "react";
import { distance, duration, ease, stagger as staggerTokens, staggerDelay } from "./tokens";
import { usePrefersReducedMotion } from "./use-reduced-motion";

type DivProps = Omit<
  ComponentPropsWithoutRef<"div">,
  "onDrag" | "onDragStart" | "onDragEnd" | "onAnimationStart" | "onAnimationEnd"
>;

const itemVariants: Variants = {
  hidden: { opacity: 0, y: distance.rise },
  shown: (index: number) => ({
    opacity: 1,
    y: 0,
    transition: { duration: duration.base, ease: ease.tide, delay: staggerDelay(index) },
  }),
};

export type StaggerProps = DivProps & { children: ReactNode; step?: number };

/**
 * Children enter one after another. Only the first `stagger.maxItems` are delayed, so a long
 * list of results never makes people wait.
 */
export function Stagger({ children, step = staggerTokens.step, ...rest }: StaggerProps) {
  const reduced = usePrefersReducedMotion();
  if (reduced) return <div {...rest}>{children}</div>;
  return (
    <motion.div {...rest} initial="hidden" animate="shown" transition={{ staggerChildren: step }}>
      {Children.map(children, (child, index) => (
        <motion.div custom={index} variants={itemVariants}>
          {child}
        </motion.div>
      ))}
    </motion.div>
  );
}
