import type { InputHTMLAttributes, ReactNode, Ref, TextareaHTMLAttributes } from "react";
import { cn } from "../lib/cn";
import { useFieldControl } from "./Field";

export const controlBase =
  "w-full bg-surface text-ink placeholder:text-ink-muted shadow-[inset_0_0_0_1px_var(--color-line-strong)] " +
  "transition-[box-shadow,background-color] duration-[var(--duration-quick)] ease-[var(--ease-calm)] " +
  "hover:shadow-[inset_0_0_0_1px_var(--color-ocean-500)] " +
  "focus-visible:shadow-[inset_0_0_0_2px_var(--color-lagoon-500)] focus-visible:outline-none " +
  "aria-[invalid=true]:shadow-[inset_0_0_0_2px_var(--color-danger)] " +
  "disabled:cursor-not-allowed disabled:bg-surface-sunken disabled:text-ink-muted";

export type InputProps = Omit<InputHTMLAttributes<HTMLInputElement>, "size"> & {
  /** Element rendered inside the leading edge (icon, currency code). */
  start?: ReactNode;
  /** Element rendered inside the trailing edge (icon button, unit). */
  end?: ReactNode;
  size?: "md" | "lg";
  ref?: Ref<HTMLInputElement>;
};

export function Input({ start, end, size = "md", className, ref, id, ...rest }: InputProps) {
  const field = useFieldControl({ id });
  return (
    <div className={cn("relative flex items-center", className)}>
      {start ? (
        <span className="pointer-events-none absolute inset-s-3 flex items-center text-ink-muted">
          {start}
        </span>
      ) : null}
      <input
        ref={ref}
        {...field}
        {...rest}
        disabled={rest.disabled ?? field.disabled}
        className={cn(
          controlBase,
          "rounded-sm text-body",
          size === "lg" ? "h-12" : "h-10",
          start ? "ps-10" : "ps-3",
          end ? "pe-10" : "pe-3",
        )}
      />
      {end ? <span className="absolute inset-e-1.5 flex items-center">{end}</span> : null}
    </div>
  );
}

export type TextareaProps = TextareaHTMLAttributes<HTMLTextAreaElement> & {
  ref?: Ref<HTMLTextAreaElement>;
};

export function Textarea({ className, ref, id, rows = 4, ...rest }: TextareaProps) {
  const field = useFieldControl({ id });
  return (
    <textarea
      ref={ref}
      rows={rows}
      {...field}
      {...rest}
      disabled={rest.disabled ?? field.disabled}
      className={cn(controlBase, "resize-y rounded-sm p-3 text-body", className)}
    />
  );
}
