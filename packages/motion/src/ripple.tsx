"use client";

import { AnimatePresence, motion } from "motion/react";
import { useCallback, useRef, useState, type PointerEvent, type ReactNode } from "react";
import { duration, ease } from "./tokens";
import { usePrefersReducedMotion } from "./use-reduced-motion";

type Drop = { id: number; x: number; y: number; size: number };

export type RippleProps = {
  children: ReactNode;
  className?: string;
  /** Ripple colour; defaults to currentColor at low opacity. */
  color?: string;
};

/**
 * Water-drop feedback from the press point. The container clips the ripple; the children keep
 * their own semantics (put this INSIDE or AROUND the interactive element, never instead of it).
 */
export function Ripple({ children, className, color = "currentColor" }: RippleProps) {
  const reduced = usePrefersReducedMotion();
  const [drops, setDrops] = useState<Drop[]>([]);
  const nextId = useRef(0);

  const onPointerDown = useCallback((event: PointerEvent<HTMLSpanElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    const size = Math.hypot(rect.width, rect.height) * 2;
    const id = nextId.current++;
    setDrops((current) => [
      ...current.slice(-3),
      { id, x: event.clientX - rect.left, y: event.clientY - rect.top, size },
    ]);
  }, []);

  const remove = (id: number) => setDrops((current) => current.filter((d) => d.id !== id));

  return (
    <span
      className={className}
      style={{ position: "relative", display: "inline-flex", overflow: "hidden" }}
      onPointerDown={reduced ? undefined : onPointerDown}
    >
      {children}
      <AnimatePresence>
        {drops.map((drop) => (
          <motion.span
            key={drop.id}
            aria-hidden="true"
            initial={{ scale: 0, opacity: 0.18 }}
            animate={{ scale: 1, opacity: 0 }}
            transition={{ duration: duration.slow, ease: ease.tide }}
            onAnimationComplete={() => remove(drop.id)}
            style={{
              position: "absolute",
              insetInlineStart: drop.x - drop.size / 2,
              insetBlockStart: drop.y - drop.size / 2,
              inlineSize: drop.size,
              blockSize: drop.size,
              borderRadius: "50%",
              background: color,
              pointerEvents: "none",
            }}
          />
        ))}
      </AnimatePresence>
    </span>
  );
}

export type BloomProps = { show: boolean; className?: string; children: ReactNode };

/** A one-shot soft scale-in for success confirmations (tick, "copied", "booked"). */
export function Bloom({ show, className, children }: BloomProps) {
  const reduced = usePrefersReducedMotion();
  if (reduced) return show ? <span className={className}>{children}</span> : null;
  return (
    <AnimatePresence initial={false}>
      {show ? (
        <motion.span
          className={className}
          initial={{ scale: 0.6, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          exit={{ scale: 0.9, opacity: 0, transition: { duration: duration.quick } }}
          transition={{ type: "spring", stiffness: 380, damping: 22 }}
          style={{ display: "inline-flex" }}
        >
          {children}
        </motion.span>
      ) : null}
    </AnimatePresence>
  );
}
