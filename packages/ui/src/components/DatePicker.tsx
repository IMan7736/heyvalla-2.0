import * as Popover from "@radix-ui/react-popover";
import { useState } from "react";
import { DayPicker, type Locale } from "react-day-picker";
import { Icon } from "../icons/Icon";
import { cn } from "../lib/cn";
import { intlLocale } from "../lib/format";
import { useFieldControl } from "./Field";
import { controlBase } from "./Input";

/** Dates cross the API as local calendar dates (Indian/Maldives): "YYYY-MM-DD". */
export function parseYmd(ymd: string): Date {
  const [y, m, d] = ymd.split("-").map(Number);
  return new Date(y ?? 1970, (m ?? 1) - 1, d ?? 1);
}
export function toYmd(date: Date): string {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${p(date.getMonth() + 1)}-${p(date.getDate())}`;
}

export type DatePickerProps = {
  /** "YYYY-MM-DD" or null. */
  value: string | null;
  onValueChange: (value: string | null) => void;
  placeholder: string;
  /** Earliest selectable date, "YYYY-MM-DD". */
  min?: string;
  max?: string;
  locale?: string;
  /** date-fns locale object from "react-day-picker/locale" for month and weekday names. */
  calendarLocale?: Locale;
  labels: { dialog: string; previousMonth: string; nextMonth: string };
  id?: string;
  size?: "md" | "lg";
  className?: string;
};

export function DatePicker({
  value,
  onValueChange,
  placeholder,
  min,
  max,
  locale = "en",
  calendarLocale,
  labels,
  id,
  size = "md",
  className,
}: DatePickerProps) {
  const field = useFieldControl({ id });
  const [open, setOpen] = useState(false);
  const selected = value ? parseYmd(value) : undefined;
  const text = selected
    ? new Intl.DateTimeFormat(intlLocale(locale), {
        weekday: "short",
        day: "numeric",
        month: "short",
        year: "numeric",
      }).format(selected)
    : null;

  return (
    <Popover.Root open={open} onOpenChange={setOpen}>
      <Popover.Trigger asChild>
        <button
          type="button"
          {...field}
          aria-haspopup="dialog"
          className={cn(
            controlBase,
            "flex items-center gap-2 rounded-sm px-3 text-start text-body",
            size === "lg" ? "h-12" : "h-10",
            !text && "text-ink-muted",
            className,
          )}
        >
          <Icon name="calendar" size={18} className="text-ink-muted" />
          <span className="tabular flex-1 truncate">{text ?? placeholder}</span>
        </button>
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Content
          align="start"
          sideOffset={6}
          aria-label={labels.dialog}
          className="z-50 rounded-lg bg-surface p-3 shadow-[var(--shadow-hairline),var(--shadow-float)] animate-[dh-pop-in_var(--duration-base)_var(--ease-calm)]"
        >
          <DayPicker
            mode="single"
            selected={selected}
            defaultMonth={selected}
            onSelect={(d) => {
              onValueChange(d ? toYmd(d) : null);
              if (d) setOpen(false);
            }}
            disabled={[
              ...(min ? [{ before: parseYmd(min) }] : []),
              ...(max ? [{ after: parseYmd(max) }] : []),
            ]}
            locale={calendarLocale}
            weekStartsOn={6}
            labels={{
              labelPrevious: () => labels.previousMonth,
              labelNext: () => labels.nextMonth,
            }}
            classNames={{
              root: "relative",
              months: "flex flex-col",
              month: "flex flex-col gap-2",
              month_caption: "flex h-9 items-center justify-center font-display text-lead",
              caption_label: "font-medium",
              nav: "absolute inset-x-0 top-0 flex h-9 items-center justify-between",
              button_previous:
                "grid size-9 place-items-center rounded-sm text-ink hover:bg-lagoon-50 rtl:rotate-180",
              button_next:
                "grid size-9 place-items-center rounded-sm text-ink hover:bg-lagoon-50 rtl:rotate-180",
              chevron: "fill-current",
              month_grid: "border-separate border-spacing-y-0.5",
              weekday: "size-10 text-center text-caption font-medium text-ink-muted",
              day: "size-10 p-0 text-center",
              day_button:
                "tabular size-10 rounded-full text-small text-ink transition-colors hover:bg-lagoon-50 disabled:cursor-not-allowed",
              selected: "[&>button]:!bg-action [&>button]:!text-ink-inverse [&>button]:font-medium",
              today: "[&>button]:shadow-[inset_0_0_0_1.5px_var(--color-coral)]",
              outside: "opacity-40",
              disabled: "opacity-30",
            }}
          />
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  );
}
