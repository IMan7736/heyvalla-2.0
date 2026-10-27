"use client";

import { useEffect, useRef } from "react";

export type ShortcutDef = {
  /** Stable id, also the message key suffix: motion.shortcuts.<id>. */
  id: string;
  /** "mod+k" (mod = Cmd on Mac, Ctrl elsewhere), "?", "g h" (sequence). */
  keys: string;
  /** Section in the help dialog. */
  group: "global" | "navigation" | "planner";
};

/** The single map of global shortcuts. The help dialog renders from this. */
export const SHORTCUTS: readonly ShortcutDef[] = [
  { id: "palette", keys: "mod+k", group: "global" },
  { id: "help", keys: "?", group: "global" },
  { id: "goHome", keys: "g h", group: "navigation" },
  { id: "goTrips", keys: "g t", group: "navigation" },
  { id: "goSearch", keys: "g s", group: "navigation" },
] as const;

/** True when typing should win over shortcuts (inputs, textareas, selects, contenteditable). */
export function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable) return true;
  const tag = target.tagName;
  return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";
}

type KeyLike = Pick<KeyboardEvent, "key" | "ctrlKey" | "metaKey" | "altKey" | "shiftKey">;

/** Does a single-step combo ("mod+k", "?") match this key event? */
export function matchesCombo(event: KeyLike, combo: string, isMac: boolean): boolean {
  const parts = combo.toLowerCase().split("+");
  const key = parts[parts.length - 1] ?? "";
  const wantsMod = parts.includes("mod");
  const modDown = isMac ? event.metaKey : event.ctrlKey;
  if (wantsMod !== modDown) return false;
  if (event.altKey) return false;
  if (!wantsMod && (event.ctrlKey || event.metaKey)) return false;
  return event.key.toLowerCase() === key;
}

export function detectMac(
  platform: string = typeof navigator === "undefined" ? "" : navigator.platform,
): boolean {
  return /mac|iphone|ipad/i.test(platform);
}

/** How long the second key of a "g x" sequence may take. */
export const SEQUENCE_TIMEOUT_MS = 1000;

export type ShortcutHandler = (event: KeyboardEvent) => void;

/**
 * Registers a combo ("mod+k", "?") or a two-key sequence ("g h") on window.
 * Single-key and sequence shortcuts are ignored while typing; "mod+" combos always fire.
 */
export function useShortcut(keys: string, handler: ShortcutHandler, enabled = true): void {
  const handlerRef = useRef(handler);
  useEffect(() => {
    handlerRef.current = handler;
  });

  useEffect(() => {
    if (!enabled) return;
    const isMac = detectMac();
    const steps = keys.split(" ");
    let progress = 0;
    let timer: number | undefined;

    const reset = () => {
      progress = 0;
      window.clearTimeout(timer);
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || event.repeat) return;
      const combo = steps[progress] ?? "";
      const isModCombo = combo.includes("mod+");
      if (!isModCombo && isTypingTarget(event.target)) return reset();
      if (matchesCombo(event, combo, isMac)) {
        if (progress === steps.length - 1) {
          reset();
          event.preventDefault();
          handlerRef.current(event);
        } else {
          progress += 1;
          window.clearTimeout(timer);
          timer = window.setTimeout(reset, SEQUENCE_TIMEOUT_MS);
        }
      } else if (progress > 0) {
        reset();
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.clearTimeout(timer);
    };
  }, [keys, enabled]);
}
