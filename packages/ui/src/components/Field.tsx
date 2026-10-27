import { createContext, useContext, useId, type ReactNode } from "react";
import { Icon } from "../icons/Icon";
import { cn } from "../lib/cn";

type FieldContextValue = {
  id: string;
  hintId: string;
  errorId: string;
  hasHint: boolean;
  hasError: boolean;
  required: boolean;
  disabled: boolean;
};

const FieldContext = createContext<FieldContextValue | null>(null);

/** Props every form control spreads so it is wired to its Field (label, hint, error). */
export function useFieldControl(overrides: { id?: string } = {}) {
  const ctx = useContext(FieldContext);
  if (!ctx) return { id: overrides.id } as const;
  const describedBy = [ctx.hasHint ? ctx.hintId : null, ctx.hasError ? ctx.errorId : null]
    .filter(Boolean)
    .join(" ");
  return {
    id: overrides.id ?? ctx.id,
    "aria-describedby": describedBy || undefined,
    "aria-invalid": ctx.hasError || undefined,
    "aria-required": ctx.required || undefined,
    disabled: ctx.disabled || undefined,
  } as const;
}

export type FieldProps = {
  label: ReactNode;
  hint?: ReactNode;
  /** Error message; when present the control is marked invalid. */
  error?: ReactNode;
  required?: boolean;
  disabled?: boolean;
  /** Text announced for the required marker, e.g. "required". Visible asterisk is decorative. */
  requiredLabel?: string;
  /** Visually hide the label (it stays available to screen readers). */
  hideLabel?: boolean;
  className?: string;
  children: ReactNode;
};

export function Field({
  label,
  hint,
  error,
  required = false,
  disabled = false,
  requiredLabel,
  hideLabel,
  className,
  children,
}: FieldProps) {
  const id = useId();
  const value: FieldContextValue = {
    id: `${id}-control`,
    hintId: `${id}-hint`,
    errorId: `${id}-error`,
    hasHint: Boolean(hint),
    hasError: Boolean(error),
    required,
    disabled,
  };
  return (
    <FieldContext.Provider value={value}>
      <div className={cn("flex min-w-0 flex-col gap-1.5", className)}>
        <label
          htmlFor={value.id}
          className={cn("text-small font-medium text-ink", hideLabel && "sr-only")}
        >
          {label}
          {required ? (
            <>
              <span aria-hidden="true" className="ms-1 text-coral-700">
                *
              </span>
              {requiredLabel ? <span className="sr-only"> ({requiredLabel})</span> : null}
            </>
          ) : null}
        </label>
        {children}
        {hint && !error ? (
          <p id={value.hintId} className="text-caption text-ink-muted">
            {hint}
          </p>
        ) : null}
        {error ? (
          <p id={value.errorId} className="flex items-start gap-1.5 text-caption text-danger">
            <Icon name="alert" size={14} className="mt-0.5" />
            <span>{error}</span>
          </p>
        ) : null}
      </div>
    </FieldContext.Provider>
  );
}
