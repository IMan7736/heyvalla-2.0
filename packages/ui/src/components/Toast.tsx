import * as RadixToast from "@radix-ui/react-toast";
import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { Icon } from "../icons/Icon";
import type { IconName } from "../icons/icons";
import { cn } from "../lib/cn";

export type ToastTone = "info" | "success" | "caution" | "danger";
export type ToastInput = {
  title: string;
  description?: string;
  tone?: ToastTone;
  /** ms before auto-dismiss. Default 6000; danger toasts stay 10000. */
  duration?: number;
  action?: { label: string; onClick: () => void };
};
type ToastItem = ToastInput & { id: number };

const ToastContext = createContext<((t: ToastInput) => void) | null>(null);

/** Imperative toasts: `const toast = useToast(); toast({ title })`. Needs <ToastProvider> above. */
export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used inside <ToastProvider>");
  return ctx;
}

const tones: Record<ToastTone, { icon: IconName; bar: string; iconClass: string }> = {
  info: { icon: "info", bar: "bg-lagoon-500", iconClass: "text-lagoon-600" },
  success: { icon: "check-circle", bar: "bg-success", iconClass: "text-success" },
  caution: { icon: "alert", bar: "bg-caution", iconClass: "text-caution" },
  danger: { icon: "alert", bar: "bg-danger", iconClass: "text-danger" },
};

export type ToastCardProps = ToastInput & {
  closeLabel: string;
  /** Radix toast parts or plain elements: the card is purely presentational. */
  className?: string;
};

/**
 * Presentational toast surface (tone bar, icon, title, description, action). Used by
 * ToastProvider below and available to other toast hosts (e.g. the app-wide store in
 * apps/web/src/motion) so every toast in the product looks the same.
 */
export function ToastCardContent({
  title,
  description,
  tone = "info",
  action,
  closeLabel,
}: Omit<ToastCardProps, "duration" | "className">) {
  const t = tones[tone];
  return (
    <>
      <span aria-hidden="true" className={cn("absolute inset-y-0 inset-s-0 w-1.5", t.bar)} />
      <Icon name={t.icon} size={20} className={cn("mt-0.5", t.iconClass)} />
      <div className="min-w-0 flex-1">
        <RadixToast.Title className="text-small font-medium text-ink">{title}</RadixToast.Title>
        {description ? (
          <RadixToast.Description className="mt-0.5 text-small text-ink-muted">
            {description}
          </RadixToast.Description>
        ) : null}
        {action ? (
          <RadixToast.Action altText={action.label} asChild>
            <button
              type="button"
              onClick={action.onClick}
              className="mt-2 text-small font-medium text-link underline underline-offset-4"
            >
              {action.label}
            </button>
          </RadixToast.Action>
        ) : null}
      </div>
      <RadixToast.Close
        aria-label={closeLabel}
        className="grid size-7 place-items-center rounded-full text-ink-muted hover:bg-ocean/8"
      >
        <Icon name="x" size={14} />
      </RadixToast.Close>
    </>
  );
}

export const toastCardClass =
  "relative flex w-full items-start gap-3 overflow-hidden rounded-lg bg-surface p-4 ps-5 shadow-[var(--shadow-hairline),var(--shadow-float)] data-[state=open]:animate-[dh-slide-in-end_var(--duration-slow)_var(--ease-calm)] data-[state=closed]:animate-[dh-fade-in_var(--duration-quick)_reverse_forwards]";

export function ToastProvider({
  children,
  closeLabel,
  regionLabel,
}: {
  children: ReactNode;
  closeLabel: string;
  /** Accessible name of the notifications region. */
  regionLabel: string;
}) {
  const [items, setItems] = useState<ToastItem[]>([]);
  const push = useCallback((t: ToastInput) => {
    setItems((prev) => [...prev.slice(-3), { ...t, id: Date.now() + Math.random() }]);
  }, []);
  const value = useMemo(() => push, [push]);

  return (
    <ToastContext.Provider value={value}>
      <RadixToast.Provider label={regionLabel} swipeDirection="right">
        {children}
        {items.map(({ id, duration, ...t }) => (
          <RadixToast.Root
            key={id}
            duration={duration ?? (t.tone === "danger" ? 10000 : 6000)}
            type={t.tone === "danger" ? "foreground" : "background"}
            onOpenChange={(open) => {
              if (!open) setItems((prev) => prev.filter((x) => x.id !== id));
            }}
            className={toastCardClass}
          >
            <ToastCardContent {...t} closeLabel={closeLabel} />
          </RadixToast.Root>
        ))}
        <RadixToast.Viewport className="fixed inset-be-6 inset-e-6 z-[60] flex w-96 max-w-[calc(100vw-3rem)] flex-col gap-3 outline-none" />
      </RadixToast.Provider>
    </ToastContext.Provider>
  );
}
