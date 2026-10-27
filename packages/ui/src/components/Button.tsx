import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";
import type { ButtonHTMLAttributes, ReactNode, Ref } from "react";
import { Icon } from "../icons/Icon";
import type { IconName } from "../icons/icons";
import { cn } from "../lib/cn";
import { Spinner } from "./Spinner";

export const buttonStyles = cva(
  [
    "relative inline-flex select-none items-center justify-center gap-2 whitespace-nowrap font-medium",
    "transition-[background-color,box-shadow,transform,color] duration-[var(--duration-quick)] ease-[var(--ease-calm)]",
    "active:translate-y-px disabled:pointer-events-none disabled:opacity-50 aria-disabled:pointer-events-none aria-disabled:opacity-50",
  ],
  {
    variants: {
      variant: {
        primary:
          "bg-action text-ink-inverse shadow-[var(--shadow-rest),inset_0_1px_0_rgb(255_255_255/0.18)] hover:bg-action-hover active:bg-action-press",
        secondary:
          "bg-surface text-ink shadow-[var(--shadow-hairline),var(--shadow-rest)] hover:bg-lagoon-50 hover:shadow-[0_0_0_1px_var(--color-lagoon-300),var(--shadow-rest)]",
        ghost: "bg-transparent text-ink hover:bg-ocean/8 active:bg-ocean/12",
        accent:
          "bg-coral text-ocean-950 shadow-[var(--shadow-rest),inset_0_1px_0_rgb(255_255_255/0.3)] hover:bg-coral-300",
        danger:
          "bg-danger text-ink-inverse shadow-[var(--shadow-rest)] hover:brightness-95 active:brightness-90",
        link: "bg-transparent text-link underline decoration-lagoon-300 decoration-2 underline-offset-4 hover:decoration-lagoon-600",
      },
      size: {
        sm: "h-8 rounded-sm px-3 text-small",
        md: "h-10 rounded-md px-4 text-small",
        lg: "h-12 rounded-md px-6 text-body",
        xl: "h-14 rounded-lg px-8 text-lead",
      },
      fullWidth: { true: "w-full", false: "" },
    },
    compoundVariants: [{ variant: "link", class: "h-auto rounded-xs px-0 shadow-none" }],
    defaultVariants: { variant: "primary", size: "md", fullWidth: false },
  },
);

export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> &
  VariantProps<typeof buttonStyles> & {
    /** Render the child element (e.g. a Next.js Link) with button styling. */
    asChild?: boolean;
    loading?: boolean;
    leadingIcon?: IconName;
    trailingIcon?: IconName;
    ref?: Ref<HTMLButtonElement>;
    children?: ReactNode;
  };

export function Button({
  variant,
  size,
  fullWidth,
  asChild,
  loading,
  leadingIcon,
  trailingIcon,
  className,
  children,
  disabled,
  type = "button",
  ref,
  ...rest
}: ButtonProps) {
  const classes = cn(buttonStyles({ variant, size, fullWidth }), className);
  if (asChild) {
    return (
      <Slot className={classes} ref={ref as Ref<HTMLElement>} {...(rest as object)}>
        {children}
      </Slot>
    );
  }
  const iconSize = size === "sm" ? 16 : size === "xl" ? 24 : 20;
  return (
    <button
      ref={ref}
      type={type}
      className={classes}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      {...rest}
    >
      {loading ? (
        <Spinner size={iconSize} decorative />
      ) : leadingIcon ? (
        <Icon name={leadingIcon} size={iconSize} />
      ) : null}
      {children}
      {trailingIcon && !loading ? <Icon name={trailingIcon} size={iconSize} directional /> : null}
    </button>
  );
}
