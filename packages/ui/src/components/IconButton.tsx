import { cva, type VariantProps } from "class-variance-authority";
import type { ButtonHTMLAttributes, Ref } from "react";
import { Icon } from "../icons/Icon";
import type { IconName } from "../icons/icons";
import { cn } from "../lib/cn";

const iconButtonStyles = cva(
  "inline-flex shrink-0 items-center justify-center transition-[background-color,box-shadow,color] duration-[var(--duration-quick)] ease-[var(--ease-calm)] disabled:pointer-events-none disabled:opacity-50",
  {
    variants: {
      variant: {
        ghost: "text-ink hover:bg-ocean/8 active:bg-ocean/12",
        soft: "bg-lagoon-50 text-lagoon-700 hover:bg-lagoon-100",
        outline: "bg-surface text-ink shadow-[var(--shadow-hairline)] hover:bg-lagoon-50",
        solid: "bg-action text-ink-inverse hover:bg-action-hover",
      },
      size: { sm: "size-8 rounded-sm", md: "size-10 rounded-md", lg: "size-12 rounded-md" },
    },
    defaultVariants: { variant: "ghost", size: "md" },
  },
);

export type IconButtonProps = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "aria-label"> &
  VariantProps<typeof iconButtonStyles> & {
    icon: IconName;
    /** Required: icon-only buttons need an accessible name. */
    label: string;
    directional?: boolean;
    ref?: Ref<HTMLButtonElement>;
  };

export function IconButton({
  icon,
  label,
  variant,
  size,
  directional,
  className,
  type = "button",
  ref,
  ...rest
}: IconButtonProps) {
  return (
    <button
      ref={ref}
      type={type}
      aria-label={label}
      className={cn(iconButtonStyles({ variant, size }), className)}
      {...rest}
    >
      <Icon
        name={icon}
        size={size === "sm" ? 16 : size === "lg" ? 24 : 20}
        directional={directional}
      />
    </button>
  );
}
