"use client";

import { MotionConfig } from "@dhathuru/motion";
import type { ReactNode } from "react";
import { CommandPalette } from "./command-palette";
import { Toaster } from "./toaster";
import "./motion.css";
import "@dhathuru/motion/styles.css";

/**
 * Mount once in the root layout, inside NextIntlClientProvider:
 *   <MotionProvider>{children}</MotionProvider>
 * Adds the global layer: command palette + shortcuts, toasts, motion CSS, and makes every
 * Motion animation honour the OS reduced-motion setting (transforms off, opacity kept).
 */
export function MotionProvider({ children }: { children: ReactNode }) {
  return (
    <MotionConfig reducedMotion="user">
      {children}
      <CommandPalette />
      <Toaster />
    </MotionConfig>
  );
}
