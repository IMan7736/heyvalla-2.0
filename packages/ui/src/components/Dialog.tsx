import * as RadixDialog from "@radix-ui/react-dialog";
import type { ReactNode } from "react";
import { Icon } from "../icons/Icon";
import { cn } from "../lib/cn";

export const DialogRoot = RadixDialog.Root;
export const DialogTrigger = RadixDialog.Trigger;
export const DialogClose = RadixDialog.Close;

export type DialogProps = {
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  title: ReactNode;
  description?: ReactNode;
  /** Required accessible label for the corner close button. */
  closeLabel: string;
  /** Pin actions under the content. */
  footer?: ReactNode;
  children?: ReactNode;
  size?: "sm" | "md" | "lg";
  /** "sheet" slides in from the inline-end edge (filters, details). */
  variant?: "dialog" | "sheet";
  trigger?: ReactNode;
};

const sizes = { sm: "max-w-md", md: "max-w-xl", lg: "max-w-3xl" } as const;

export function Dialog({
  open,
  onOpenChange,
  title,
  description,
  closeLabel,
  footer,
  children,
  size = "md",
  variant = "dialog",
  trigger,
}: DialogProps) {
  return (
    <RadixDialog.Root open={open} onOpenChange={onOpenChange}>
      {trigger ? <RadixDialog.Trigger asChild>{trigger}</RadixDialog.Trigger> : null}
      <RadixDialog.Portal>
        <RadixDialog.Overlay className="fixed inset-0 z-40 bg-ocean-950/45 backdrop-blur-[2px] animate-[dh-fade-in_var(--duration-base)_var(--ease-calm)]" />
        <RadixDialog.Content
          {...(description ? {} : { "aria-describedby": undefined })}
          className={cn(
            "fixed z-50 flex max-h-[calc(100dvh-4rem)] flex-col bg-surface shadow-[var(--shadow-hairline),var(--shadow-overlay)] focus-visible:outline-none",
            variant === "dialog"
              ? cn(
                  "inset-s-1/2 top-1/2 w-[calc(100%-4rem)] -translate-x-1/2 -translate-y-1/2 rounded-xl rtl:translate-x-1/2 animate-[dh-rise-in_var(--duration-base)_var(--ease-calm)]",
                  sizes[size],
                )
              : "inset-y-0 inset-e-0 max-h-none w-[26rem] rounded-s-xl animate-[dh-slide-in-end_var(--duration-slow)_var(--ease-calm)]",
          )}
        >
          <div className="flex items-start justify-between gap-4 p-6 pb-2">
            <div className="flex flex-col gap-1">
              <RadixDialog.Title className="font-display text-title text-ink">
                {title}
              </RadixDialog.Title>
              {description ? (
                <RadixDialog.Description className="text-small text-ink-muted">
                  {description}
                </RadixDialog.Description>
              ) : null}
            </div>
            <RadixDialog.Close
              aria-label={closeLabel}
              className="grid size-9 shrink-0 place-items-center rounded-full text-ink-muted hover:bg-ocean/8 hover:text-ink"
            >
              <Icon name="x" size={18} />
            </RadixDialog.Close>
          </div>
          <div className="min-h-0 flex-1 overflow-auto px-6 py-4">{children}</div>
          {footer ? (
            <div className="flex justify-end gap-3 border-t border-line p-4 ps-6 pe-6">
              {footer}
            </div>
          ) : null}
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  );
}
