"use client";

import { Icon, cn } from "@dhathuru/ui";
import {
  AnimatePresence,
  motion,
  duration,
  ease,
  spring,
  usePrefersReducedMotion,
} from "@dhathuru/motion";
import { useTranslations } from "next-intl";
import { useEffect } from "react";
import { dismissToast, useToasts, type Toast } from "./toast-store";

const tones = {
  info: { icon: "info", bar: "bg-lagoon-500", iconClass: "text-lagoon-600" },
  success: { icon: "check-circle", bar: "bg-success", iconClass: "text-success" },
  caution: { icon: "alert", bar: "bg-caution", iconClass: "text-caution" },
  danger: { icon: "alert", bar: "bg-danger", iconClass: "text-danger" },
} as const;

function ToastItem({ toast }: { toast: Toast }) {
  const t = useTranslations("motion.toast");
  const reduced = usePrefersReducedMotion();
  const tone = tones[toast.tone];

  useEffect(() => {
    if (toast.duration <= 0) return;
    const timer = window.setTimeout(() => dismissToast(toast.id), toast.duration);
    return () => window.clearTimeout(timer);
  }, [toast.id, toast.duration]);

  return (
    <motion.li
      layout={!reduced}
      initial={reduced ? false : { opacity: 0, y: 16, scale: 0.97 }}
      animate={{ opacity: 1, y: 0, scale: 1, transition: reduced ? { duration: 0 } : spring.soft }}
      exit={{ opacity: 0, transition: { duration: reduced ? 0 : duration.quick, ease: ease.ebb } }}
      className="pointer-events-auto relative flex w-full items-start gap-3 overflow-hidden rounded-lg bg-surface p-4 ps-5 shadow-[var(--shadow-hairline),var(--shadow-float)]"
    >
      <span aria-hidden="true" className={cn("absolute inset-y-0 inset-s-0 w-1.5", tone.bar)} />
      <Icon name={tone.icon} size={20} className={cn("mt-0.5", tone.iconClass)} />
      <div className="min-w-0 flex-1">
        <p className="text-small font-medium text-ink">{toast.title}</p>
        {toast.description ? (
          <p className="mt-0.5 text-small text-ink-muted">{toast.description}</p>
        ) : null}
        {toast.action ? (
          <button
            type="button"
            className="mt-2 text-small font-medium text-link underline underline-offset-4"
            onClick={() => {
              toast.action?.onClick();
              dismissToast(toast.id);
            }}
          >
            {toast.action.label}
          </button>
        ) : null}
      </div>
      <button
        type="button"
        aria-label={t("dismiss")}
        className="grid size-7 place-items-center rounded-full text-ink-muted hover:bg-ocean/8"
        onClick={() => dismissToast(toast.id)}
      >
        <Icon name="x" size={14} />
      </button>
    </motion.li>
  );
}

/**
 * Fallback toast stack for pages that have no <ToastBridge> (no UiProviders above). Styled with
 * the same tokens and structure as the UI kit's toast card; once a bridge registers, toasts go
 * to the UI kit host and this renders nothing.
 */
export function Toaster() {
  const t = useTranslations("motion.toast");
  const toasts = useToasts();
  if (toasts.length === 0) return null;
  return (
    <div
      role="region"
      aria-label={t("region")}
      aria-live="polite"
      className="pointer-events-none fixed bottom-6 z-[60] w-96 max-w-[calc(100vw-3rem)]"
      style={{ insetInlineEnd: "1.5rem" }}
    >
      <ul className="flex flex-col gap-3">
        <AnimatePresence initial={false}>
          {toasts.map((toast) => (
            <ToastItem key={toast.id} toast={toast} />
          ))}
        </AnimatePresence>
      </ul>
    </div>
  );
}
