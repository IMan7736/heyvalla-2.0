"use client";

import { useSyncExternalStore } from "react";

export type PaletteView = "commands" | "help";
export type PaletteState = { open: boolean; view: PaletteView };

let state: PaletteState = { open: false, view: "commands" };
const listeners = new Set<() => void>();

function set(next: PaletteState): void {
  if (next.open === state.open && next.view === state.view) return;
  state = next;
  for (const listener of listeners) listener();
}

/** Element that had focus when the palette opened; focus returns there on close. */
let opener: HTMLElement | null = null;

export function openPalette(view: PaletteView = "commands"): void {
  if (!state.open && typeof document !== "undefined") {
    opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  }
  set({ open: true, view });
}
export function closePalette(): void {
  const wasOpen = state.open;
  set({ open: false, view: "commands" });
  const target = opener;
  opener = null;
  if (wasOpen && target) {
    // After the dialog has unmounted, so the dialog's own focus handling cannot win.
    window.setTimeout(() => {
      if (target.isConnected && document.activeElement === document.body) target.focus();
    }, 0);
  }
}
export function setPaletteView(view: PaletteView): void {
  set({ open: true, view });
}
export function togglePalette(): void {
  if (state.open) closePalette();
  else openPalette();
}
export function getPaletteState(): PaletteState {
  return state;
}

export function usePaletteState(): PaletteState {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => state,
    () => state,
  );
}
