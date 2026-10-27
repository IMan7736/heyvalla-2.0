import * as RadixTooltip from "@radix-ui/react-tooltip";
import type { ReactNode } from "react";

export const TooltipProvider = RadixTooltip.Provider;

export type TooltipProps = {
  content: ReactNode;
  children: ReactNode;
  side?: "top" | "bottom" | "start" | "end";
};

/** Short non-interactive hint. Also reachable by keyboard focus. Do not hide essential information here. */
export function Tooltip({ content, children, side = "top" }: TooltipProps) {
  const radixSide = side === "start" ? "left" : side === "end" ? "right" : side;
  return (
    <RadixTooltip.Root delayDuration={250}>
      <RadixTooltip.Trigger asChild>{children}</RadixTooltip.Trigger>
      <RadixTooltip.Portal>
        <RadixTooltip.Content
          side={radixSide}
          sideOffset={8}
          className="z-50 max-w-64 rounded-sm bg-ocean-900 px-3 py-1.5 text-caption text-ink-inverse shadow-[var(--shadow-float)] animate-[dh-fade-in_var(--duration-quick)_var(--ease-calm)]"
        >
          {content}
          <RadixTooltip.Arrow className="fill-ocean-900" />
        </RadixTooltip.Content>
      </RadixTooltip.Portal>
    </RadixTooltip.Root>
  );
}
