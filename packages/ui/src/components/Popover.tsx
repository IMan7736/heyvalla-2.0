import * as RadixPopover from "@radix-ui/react-popover";
import type { ComponentProps } from "react";
import { cn } from "../lib/cn";

export const Popover = RadixPopover.Root;
export const PopoverTrigger = RadixPopover.Trigger;
export const PopoverAnchor = RadixPopover.Anchor;

export function PopoverContent({
  className,
  sideOffset = 8,
  ...rest
}: ComponentProps<typeof RadixPopover.Content>) {
  return (
    <RadixPopover.Portal>
      <RadixPopover.Content
        sideOffset={sideOffset}
        className={cn(
          "z-50 w-72 rounded-lg bg-surface p-4 text-ink shadow-[var(--shadow-hairline),var(--shadow-float)] animate-[dh-pop-in_var(--duration-base)_var(--ease-calm)]",
          className,
        )}
        {...rest}
      />
    </RadixPopover.Portal>
  );
}
