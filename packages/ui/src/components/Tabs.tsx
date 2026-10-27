import * as RadixTabs from "@radix-ui/react-tabs";
import type { ComponentProps, ReactNode } from "react";
import { Icon } from "../icons/Icon";
import type { IconName } from "../icons/icons";
import { cn } from "../lib/cn";

export const Tabs = RadixTabs.Root;

export function TabsList({
  className,
  variant = "underline",
  ...rest
}: ComponentProps<typeof RadixTabs.List> & { variant?: "underline" | "segmented" }) {
  return (
    <RadixTabs.List
      className={cn(
        variant === "underline"
          ? "flex gap-6 border-b border-line"
          : "inline-flex gap-1 rounded-md bg-surface-sunken p-1",
        className,
      )}
      data-variant={variant}
      {...rest}
    />
  );
}

export function Tab({
  icon,
  children,
  className,
  ...rest
}: ComponentProps<typeof RadixTabs.Trigger> & { icon?: IconName; children: ReactNode }) {
  return (
    <RadixTabs.Trigger
      className={cn(
        "group relative inline-flex h-11 items-center gap-2 text-small font-medium text-ink-muted transition-colors hover:text-ink",
        "data-[state=active]:text-lagoon-700",
        // underline variant
        "[[data-variant=underline]_&]:after:absolute [[data-variant=underline]_&]:after:inset-x-0 [[data-variant=underline]_&]:after:-bottom-px [[data-variant=underline]_&]:after:h-0.5 [[data-variant=underline]_&]:after:scale-x-0 [[data-variant=underline]_&]:after:bg-action [[data-variant=underline]_&]:after:transition-transform [[data-variant=underline]_&]:data-[state=active]:after:scale-x-100",
        // segmented variant
        "[[data-variant=segmented]_&]:h-9 [[data-variant=segmented]_&]:rounded-sm [[data-variant=segmented]_&]:px-4 [[data-variant=segmented]_&]:data-[state=active]:bg-surface [[data-variant=segmented]_&]:data-[state=active]:shadow-[var(--shadow-rest)]",
        className,
      )}
      {...rest}
    >
      {icon ? <Icon name={icon} size={18} /> : null}
      {children}
    </RadixTabs.Trigger>
  );
}

export function TabPanel({ className, ...rest }: ComponentProps<typeof RadixTabs.Content>) {
  return <RadixTabs.Content className={cn("pt-6", className)} {...rest} />;
}
