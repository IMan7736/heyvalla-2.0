"use client";

import { ToastProvider, TooltipProvider } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { ToastBridge } from "@/motion";

/** Client-side providers for the design system (toasts, tooltips), wired to next-intl labels. */
export function UiProviders({ children }: { children: ReactNode }) {
  const t = useTranslations("ui");
  return (
    <TooltipProvider delayDuration={250}>
      <ToastProvider closeLabel={t("dismiss")} regionLabel={t("notifications")}>
        <ToastBridge />
        {children}
      </ToastProvider>
    </TooltipProvider>
  );
}
