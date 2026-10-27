import * as RadioGroup from "@radix-ui/react-radio-group";
import type { ReactNode } from "react";
import { cn } from "../lib/cn";

export type SegmentedOption = { value: string; label: ReactNode };

export type SegmentedControlProps = {
  options: SegmentedOption[];
  value: string;
  onValueChange: (value: string) => void;
  /** Accessible name for the group. */
  label: string;
  size?: "sm" | "md";
  className?: string;
};

/** Mutually exclusive choice rendered as a pill track (e.g. Depart after / Arrive by). */
export function SegmentedControl({
  options,
  value,
  onValueChange,
  label,
  size = "md",
  className,
}: SegmentedControlProps) {
  return (
    <RadioGroup.Root
      aria-label={label}
      value={value}
      onValueChange={onValueChange}
      orientation="horizontal"
      className={cn("inline-flex gap-1 rounded-md bg-surface-sunken p-1", className)}
    >
      {options.map((o) => (
        <RadioGroup.Item
          key={o.value}
          value={o.value}
          className={cn(
            "rounded-sm px-4 font-medium text-ink-muted transition-[background-color,color,box-shadow] duration-[var(--duration-quick)] hover:text-ink data-[state=checked]:bg-surface data-[state=checked]:text-lagoon-700 data-[state=checked]:shadow-[var(--shadow-rest)]",
            size === "sm" ? "h-7 text-caption" : "h-9 text-small",
          )}
        >
          {o.label}
        </RadioGroup.Item>
      ))}
    </RadioGroup.Root>
  );
}
