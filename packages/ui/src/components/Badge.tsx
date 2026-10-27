import { cva, type VariantProps } from "class-variance-authority";
import type { HTMLAttributes, ReactNode } from "react";
import { Icon } from "../icons/Icon";
import type { IconName } from "../icons/icons";
import { cn } from "../lib/cn";

const badgeStyles = cva(
  "inline-flex items-center gap-1 whitespace-nowrap font-medium leading-none",
  {
    variants: {
      tone: {
        neutral: "bg-sand-200 text-ocean-800",
        lagoon: "bg-lagoon-50 text-lagoon-700",
        solid: "bg-action text-ink-inverse",
        coral: "bg-coral-100 text-coral-700",
        success: "bg-success-soft text-success",
        caution: "bg-caution-soft text-caution",
        danger: "bg-danger-soft text-danger",
      },
      size: { sm: "h-5 rounded-xs px-1.5 text-caption", md: "h-6 rounded-xs px-2 text-small" },
      shape: { square: "", pill: "rounded-full" },
    },
    defaultVariants: { tone: "neutral", size: "md", shape: "square" },
  },
);

export type BadgeProps = HTMLAttributes<HTMLSpanElement> &
  VariantProps<typeof badgeStyles> & { icon?: IconName; children: ReactNode };

/** Compact status or category marker. Never rely on tone alone: pair with text or an icon. */
export function Badge({ tone, size, shape, icon, className, children, ...rest }: BadgeProps) {
  return (
    <span className={cn(badgeStyles({ tone, size, shape }), className)} {...rest}>
      {icon ? <Icon name={icon} size={size === "sm" ? 12 : 14} /> : null}
      {children}
    </span>
  );
}

/** Pill: rounded badge with an optional leading dot, used for trip labels (Fastest, Cheapest). */
export function Pill({
  dot,
  tone = "lagoon",
  className,
  children,
  ...rest
}: Omit<BadgeProps, "shape" | "icon"> & { dot?: boolean }) {
  return (
    <Badge tone={tone} shape="pill" className={cn("px-2.5", className)} {...rest}>
      {dot ? <span aria-hidden="true" className="size-1.5 rounded-full bg-coral" /> : null}
      {children}
    </Badge>
  );
}

/** Tag: removable/selectable chip for filters (mode filters, preferences). */
export function Tag({
  children,
  onRemove,
  removeLabel,
  className,
}: {
  children: ReactNode;
  onRemove?: () => void;
  /** Required with onRemove, e.g. "Remove ferry". */
  removeLabel?: string;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex h-8 items-center gap-1.5 rounded-full bg-surface ps-3 pe-3 text-small text-ink shadow-[var(--shadow-hairline)]",
        onRemove && "pe-1.5",
        className,
      )}
    >
      {children}
      {onRemove ? (
        <button
          type="button"
          aria-label={removeLabel}
          onClick={onRemove}
          className="grid size-5 place-items-center rounded-full text-ink-muted hover:bg-ocean/10 hover:text-ink"
        >
          <Icon name="x" size={12} />
        </button>
      ) : null}
    </span>
  );
}
