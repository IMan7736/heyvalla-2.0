import { cva, type VariantProps } from "class-variance-authority";
import type { HTMLAttributes, Ref } from "react";
import { cn } from "../lib/cn";

const cardStyles = cva("rounded-lg bg-surface text-ink", {
  variants: {
    elevation: {
      flat: "shadow-[var(--shadow-hairline)]",
      rest: "shadow-[var(--shadow-hairline),var(--shadow-rest)]",
      raised: "shadow-[var(--shadow-hairline),var(--shadow-raised)]",
      float: "shadow-[var(--shadow-hairline),var(--shadow-float)]",
    },
    padding: { none: "", sm: "p-4", md: "p-6", lg: "p-8" },
    interactive: {
      true: "transition-[box-shadow,transform] duration-[var(--duration-base)] ease-[var(--ease-calm)] hover:-translate-y-0.5 hover:shadow-[var(--shadow-hairline),var(--shadow-raised)]",
      false: "",
    },
  },
  defaultVariants: { elevation: "rest", padding: "md", interactive: false },
});

export type CardProps = HTMLAttributes<HTMLDivElement> &
  VariantProps<typeof cardStyles> & { ref?: Ref<HTMLDivElement> };

export function Card({ elevation, padding, interactive, className, ref, ...rest }: CardProps) {
  return (
    <div
      ref={ref}
      className={cn(cardStyles({ elevation, padding, interactive }), className)}
      {...rest}
    />
  );
}

export function CardHeader({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("mb-4 flex flex-col gap-1", className)} {...rest} />;
}

export function CardTitle({ className, ...rest }: HTMLAttributes<HTMLHeadingElement>) {
  return <h3 className={cn("font-display text-title text-ink", className)} {...rest} />;
}

export function CardDescription({ className, ...rest }: HTMLAttributes<HTMLParagraphElement>) {
  return <p className={cn("text-small text-ink-muted", className)} {...rest} />;
}
