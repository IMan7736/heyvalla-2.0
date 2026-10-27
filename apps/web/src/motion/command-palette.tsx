"use client";

import dynamic from "next/dynamic";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { closePalette, openPalette, usePaletteState } from "./palette-store";
import { MOTION_ROUTES } from "./routes";
import { useShortcut } from "./shortcuts";

// cmdk, the Radix dialog and the dialog UI load as a separate chunk, fetched on idle.
const PaletteDialog = dynamic(() => import("./palette-dialog"), { ssr: false });

/** Idle time after load before the invisible warm-up render (ms). */
const WARM_DELAY_MS = 1200;

type IdleWindow = Window & {
  requestIdleCallback?: (cb: () => void, options?: { timeout: number }) => number;
  cancelIdleCallback?: (id: number) => void;
};

/**
 * Global command palette (Cmd/Ctrl+K) and shortcut help (?). One dialog, two views, so focus
 * handling lives in one place: cmdk/Radix trap focus, restore it to the trigger on close and
 * close on Escape.
 */
export function CommandPalette() {
  const router = useRouter();
  const { open } = usePaletteState();
  const [warm, setWarm] = useState(false);

  useShortcut("mod+k", () => (open ? closePalette() : openPalette()));
  useShortcut("?", () => openPalette("help"));
  useShortcut("g h", () => router.push(MOTION_ROUTES.home));
  useShortcut("g s", () => router.push(MOTION_ROUTES.plan));
  useShortcut("g t", () => router.push(MOTION_ROUTES.trips));

  // Declared after the shortcut hooks, so by the time this runs the shortcuts are live. Tests and
  // tools wait for it instead of guessing when hydration has finished.
  useEffect(() => {
    document.documentElement.dataset.motionReady = "true";
    return () => {
      delete document.documentElement.dataset.motionReady;
    };
  }, []);

  // Fetch the dialog chunk as soon as the page is interactive, then warm the code paths once
  // while the browser is idle.
  useEffect(() => {
    void import("./palette-dialog");
    const idle = window as IdleWindow;
    let frame = 0;
    let stop = 0;
    const timer = window.setTimeout(() => {
      const run = () => {
        setWarm(true);
        frame = requestAnimationFrame(() => {
          frame = requestAnimationFrame(() => {
            stop = window.setTimeout(() => setWarm(false), 50);
          });
        });
      };
      if (idle.requestIdleCallback) idle.requestIdleCallback(run, { timeout: 2000 });
      else run();
    }, WARM_DELAY_MS);
    return () => {
      window.clearTimeout(timer);
      window.clearTimeout(stop);
      cancelAnimationFrame(frame);
    };
  }, []);

  // Mounted only while open (or warming), so query, results and recents start fresh every time.
  if (open) return <PaletteDialog key="live" />;
  return warm ? <PaletteDialog key="warm" warm /> : null;
}
