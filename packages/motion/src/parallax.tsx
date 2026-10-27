"use client";

import { motion, useScroll, useTransform } from "motion/react";
import { useRef, type ReactNode } from "react";
import { usePrefersReducedMotion } from "./use-reduced-motion";

export type ParallaxProps = {
  children: ReactNode;
  className?: string;
  /** Total travel in px across the element's pass through the viewport. Keep small (<= 40). */
  distance?: number;
};

/** Restrained scroll drift on transform only. Static under reduced motion. */
export function Parallax({ children, className, distance = 24 }: ParallaxProps) {
  const reduced = usePrefersReducedMotion();
  const ref = useRef<HTMLDivElement>(null);
  const { scrollYProgress } = useScroll({ target: ref, offset: ["start end", "end start"] });
  const y = useTransform(scrollYProgress, [0, 1], [distance / 2, -distance / 2]);

  if (reduced) return <div className={className}>{children}</div>;
  return (
    <motion.div ref={ref} className={className} style={{ y }}>
      {children}
    </motion.div>
  );
}
