"use client";

import { useSyncExternalStore } from "react";

/** Same shape as @dhathuru/ui's ToastInput so both systems speak one language. */
export type ToastTone = "info" | "success" | "caution" | "danger";

export type ToastInput = {
  /** Already-translated text. Callers pass t("...") results. */
  title: string;
  description?: string;
  tone?: ToastTone;
  /** ms before auto-dismiss. 0 keeps it until dismissed. Default 6000 (danger 10000), as in the UI kit. */
  duration?: number;
  action?: { label: string; onClick: () => void };
};

export type Toast = ToastInput & { id: number; tone: ToastTone; duration: number };

export const MAX_VISIBLE_TOASTS = 3;

export function defaultDuration(tone: ToastTone): number {
  return tone === "danger" ? 10000 : 6000;
}

type Listener = () => void;
type Sink = (toast: ToastInput) => void;

let toasts: readonly Toast[] = [];
let nextId = 1;
let sink: Sink | null = null;
const listeners = new Set<Listener>();

function emit(next: readonly Toast[]): void {
  toasts = next;
  for (const listener of listeners) listener();
}

/**
 * Route toasts to the design system's host (ToastBridge inside <ToastProvider>) so there is one
 * toast surface and one live region. Returns the unregister function. Without a host, the
 * fallback <Toaster> renders them from this store.
 */
export function setToastSink(next: Sink): () => void {
  sink = next;
  const pending = toasts;
  emit([]);
  for (const toast of pending) next(toast);
  return () => {
    if (sink === next) sink = null;
  };
}

/** Show a toast from anywhere (also outside React). Returns an id usable with dismissToast. */
export function showToast(input: ToastInput): number {
  const id = nextId++;
  if (sink) {
    sink(input);
    return id;
  }
  const tone = input.tone ?? "info";
  const toast: Toast = { ...input, id, tone, duration: input.duration ?? defaultDuration(tone) };
  emit([...toasts, toast].slice(-MAX_VISIBLE_TOASTS));
  return id;
}

export function dismissToast(id: number): void {
  if (toasts.some((t) => t.id === id)) emit(toasts.filter((t) => t.id !== id));
}

export function clearToasts(): void {
  if (toasts.length > 0) emit([]);
}

function subscribe(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useToasts(): readonly Toast[] {
  return useSyncExternalStore(
    subscribe,
    () => toasts,
    () => toasts,
  );
}
