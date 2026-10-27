import * as RadixAccordion from "@radix-ui/react-accordion";
import * as RadixAvatar from "@radix-ui/react-avatar";
import * as RadixProgress from "@radix-ui/react-progress";
import * as RadixSeparator from "@radix-ui/react-separator";
import type { ComponentProps, HTMLAttributes, ReactNode } from "react";
import { Icon } from "../icons/Icon";
import type { IconName } from "../icons/icons";
import { cn } from "../lib/cn";

/* ---------- Skeleton ---------- */
export function Skeleton({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      aria-hidden="true"
      className={cn("relative overflow-hidden rounded-sm bg-sand-200", className)}
      {...rest}
    >
      <div className="absolute inset-0 animate-[dh-shimmer_1.6s_var(--ease-calm)_infinite] bg-gradient-to-r from-transparent via-white/70 to-transparent rtl:bg-gradient-to-l" />
    </div>
  );
}

/* ---------- Avatar ---------- */
function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  const first = parts[0]?.[0] ?? "";
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? "") : "";
  return (first + last).toLocaleUpperCase();
}

export function Avatar({
  name,
  src,
  size = 36,
  className,
}: {
  /** Full name; used for initials and as the accessible name. */
  name: string;
  src?: string;
  size?: number;
  className?: string;
}) {
  return (
    <RadixAvatar.Root
      className={cn(
        "inline-flex shrink-0 select-none items-center justify-center overflow-hidden rounded-full bg-lagoon-100 font-medium text-lagoon-800",
        className,
      )}
      style={{ width: size, height: size, fontSize: Math.round(size * 0.38) }}
      role="img"
      aria-label={name}
    >
      {src ? <RadixAvatar.Image src={src} alt="" className="size-full object-cover" /> : null}
      <RadixAvatar.Fallback aria-hidden="true">{initialsOf(name)}</RadixAvatar.Fallback>
    </RadixAvatar.Root>
  );
}

/* ---------- Stepper (checkout / multi-step progress) ---------- */
export type StepperStep = { id: string; label: string };

export function Stepper({
  steps,
  currentId,
  label,
  completeLabel,
  currentLabel,
  className,
}: {
  steps: StepperStep[];
  currentId: string;
  /** Accessible name of the list, e.g. "Booking progress". */
  label: string;
  /** Screen-reader text for finished steps, e.g. "completed". */
  completeLabel: string;
  /** Screen-reader text for the current step, e.g. "current step". */
  currentLabel: string;
  className?: string;
}) {
  const currentIndex = Math.max(
    0,
    steps.findIndex((s) => s.id === currentId),
  );
  return (
    <ol aria-label={label} className={cn("flex items-center", className)}>
      {steps.map((s, i) => {
        const state = i < currentIndex ? "done" : i === currentIndex ? "current" : "todo";
        return (
          <li
            key={s.id}
            aria-current={state === "current" ? "step" : undefined}
            className="flex flex-1 items-center last:flex-none"
          >
            <span className="flex items-center gap-3">
              <span
                className={cn(
                  "tabular grid size-8 shrink-0 place-items-center rounded-full text-small font-medium transition-colors duration-[var(--duration-base)]",
                  state === "done" && "bg-action text-ink-inverse",
                  state === "current" &&
                    "bg-surface text-lagoon-700 shadow-[inset_0_0_0_2px_var(--color-action)]",
                  state === "todo" && "bg-sand-200 text-ink-muted",
                )}
              >
                {state === "done" ? <Icon name="check" size={16} strokeWidth={2.4} /> : i + 1}
              </span>
              <span
                className={cn(
                  "text-small",
                  state === "todo" ? "text-ink-muted" : "font-medium text-ink",
                )}
              >
                {s.label}
                {state !== "todo" ? (
                  <span className="sr-only">
                    {" "}
                    ({state === "done" ? completeLabel : currentLabel})
                  </span>
                ) : null}
              </span>
            </span>
            {i < steps.length - 1 ? (
              <span
                aria-hidden="true"
                className={cn(
                  "mx-4 h-px flex-1",
                  i < currentIndex ? "bg-action" : "bg-line-strong",
                )}
              />
            ) : null}
          </li>
        );
      })}
    </ol>
  );
}

/* ---------- Progress ---------- */
export function Progress({
  value,
  label,
  className,
}: {
  value: number;
  label: string;
  className?: string;
}) {
  const clamped = Math.max(0, Math.min(100, value));
  return (
    <RadixProgress.Root
      value={clamped}
      aria-label={label}
      className={cn("relative h-2 overflow-hidden rounded-full bg-sand-200", className)}
    >
      <RadixProgress.Indicator
        className="size-full origin-left rounded-full bg-action transition-transform duration-[var(--duration-slow)] ease-[var(--ease-calm)] rtl:origin-right"
        style={{ transform: `scaleX(${clamped / 100})` }}
      />
    </RadixProgress.Root>
  );
}

/* ---------- Separator ---------- */
export function Separator({ className, ...rest }: ComponentProps<typeof RadixSeparator.Root>) {
  return (
    <RadixSeparator.Root
      className={cn(
        "bg-line data-[orientation=horizontal]:h-px data-[orientation=horizontal]:w-full data-[orientation=vertical]:w-px data-[orientation=vertical]:self-stretch",
        className,
      )}
      {...rest}
    />
  );
}

/* ---------- Alert (inline message) ---------- */
const alertTones = {
  info: { icon: "info", box: "bg-lagoon-50 text-lagoon-800", icon_: "text-lagoon-600" },
  success: { icon: "check-circle", box: "bg-success-soft text-ocean-900", icon_: "text-success" },
  caution: { icon: "alert", box: "bg-caution-soft text-ocean-900", icon_: "text-caution" },
  danger: { icon: "alert", box: "bg-danger-soft text-ocean-900", icon_: "text-danger" },
} as const satisfies Record<string, { icon: IconName; box: string; icon_: string }>;

export function Alert({
  tone = "info",
  title,
  children,
  action,
  className,
}: {
  tone?: keyof typeof alertTones;
  title?: ReactNode;
  children?: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  const t = alertTones[tone];
  return (
    <div
      role={tone === "danger" ? "alert" : "status"}
      className={cn("flex items-start gap-3 rounded-md p-4", t.box, className)}
    >
      <Icon name={t.icon} size={20} className={cn("mt-0.5", t.icon_)} />
      <div className="min-w-0 flex-1 text-small">
        {title ? <p className="font-medium">{title}</p> : null}
        {children ? <div className={title ? "mt-0.5" : undefined}>{children}</div> : null}
      </div>
      {action}
    </div>
  );
}

/* ---------- Accordion ---------- */
export type AccordionItem = { id: string; title: ReactNode; content: ReactNode };

export function Accordion({
  items,
  className,
  headingLevel = 3,
}: {
  items: AccordionItem[];
  className?: string;
  /** Level of each item's heading; pick one level below the section heading (default 3). */
  headingLevel?: 2 | 3 | 4;
}) {
  const Heading = `h${headingLevel}` as const;
  return (
    <RadixAccordion.Root type="multiple" className={cn("divide-y divide-line", className)}>
      {items.map((it) => (
        <RadixAccordion.Item key={it.id} value={it.id}>
          <RadixAccordion.Header asChild>
            <Heading>
            <RadixAccordion.Trigger className="group flex w-full items-center justify-between gap-4 py-4 text-start text-body font-medium text-ink">
              {it.title}
              <Icon
                name="chevron-down"
                size={18}
                className="text-ink-muted transition-transform duration-[var(--duration-base)] group-data-[state=open]:rotate-180"
              />
            </RadixAccordion.Trigger>
            </Heading>
          </RadixAccordion.Header>
          <RadixAccordion.Content className="overflow-hidden pb-4 text-body text-ink-muted data-[state=open]:animate-[dh-fade-in_var(--duration-base)_var(--ease-calm)]">
            {it.content}
          </RadixAccordion.Content>
        </RadixAccordion.Item>
      ))}
    </RadixAccordion.Root>
  );
}

/* ---------- Breadcrumbs ---------- */
export function Breadcrumbs({
  items,
  label,
}: {
  items: { label: string; href?: string }[];
  /** Accessible name for the nav landmark, e.g. "Breadcrumb". */
  label: string;
}) {
  return (
    <nav aria-label={label}>
      <ol className="flex items-center gap-2 text-small text-ink-muted">
        {items.map((it, i) => (
          <li key={it.label} className="flex items-center gap-2">
            {it.href && i < items.length - 1 ? (
              <a href={it.href} className="rounded-xs text-link underline-offset-4 hover:underline">
                {it.label}
              </a>
            ) : (
              <span aria-current={i === items.length - 1 ? "page" : undefined} className="text-ink">
                {it.label}
              </span>
            )}
            {i < items.length - 1 ? <Icon name="chevron-right" size={14} directional /> : null}
          </li>
        ))}
      </ol>
    </nav>
  );
}
