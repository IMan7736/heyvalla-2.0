"use client";

import { useToast } from "@dhathuru/ui";
import { useEffect } from "react";
import { setToastSink } from "./toast-store";

/**
 * Render inside @dhathuru/ui's <ToastProvider> (see UiProviders). From then on every
 * `showToast()` call, from React or not, is shown by the design system's toast host, so
 * the product has a single toast look and a single live region. Renders nothing.
 */
export function ToastBridge() {
  const toast = useToast();
  useEffect(() => setToastSink(toast), [toast]);
  return null;
}
