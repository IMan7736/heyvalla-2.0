import * as Popover from "@radix-ui/react-popover";
import { useId, useMemo, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { Icon } from "../icons/Icon";
import type { IconName } from "../icons/icons";
import { cn } from "../lib/cn";
import { useFieldControl } from "./Field";
import { controlBase } from "./Input";
import { Spinner } from "./Spinner";

export type ComboboxOption = {
  value: string;
  label: string;
  /** Secondary line (atoll, island type). */
  description?: string;
  icon?: IconName;
};

export type ComboboxProps = {
  options: ComboboxOption[];
  /** Selected option value. */
  value?: string | null;
  onValueChange: (value: string | null) => void;
  placeholder: string;
  /** Shown when nothing matches. */
  emptyLabel: string;
  /** Screen-reader announcement, e.g. (n) => `${n} results`. */
  resultsLabel: (count: number) => string;
  /** Label for the clear button; omit to hide it. */
  clearLabel?: string;
  loading?: boolean;
  loadingLabel?: string;
  /** Called on every keystroke so callers can fetch options. Disable local filtering with `filter={false}`. */
  onQueryChange?: (query: string) => void;
  /** Filter options locally by label (default true). Set false when options are server-filtered. */
  filter?: boolean;
  id?: string;
  name?: string;
  size?: "md" | "lg";
  start?: ReactNode;
  className?: string;
};

/**
 * Accessible combobox (ARIA 1.2 "editable combobox with list autocomplete"):
 * focus stays in the input, arrow keys move aria-activedescendant, Enter selects, Escape closes.
 */
export function Combobox({
  options,
  value,
  onValueChange,
  placeholder,
  emptyLabel,
  resultsLabel,
  clearLabel,
  loading,
  loadingLabel,
  onQueryChange,
  filter = true,
  id,
  name,
  size = "md",
  start,
  className,
}: ComboboxProps) {
  const field = useFieldControl({ id });
  const listId = useId();
  const selected = options.find((o) => o.value === value) ?? null;
  const [query, setQuery] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  const text = query ?? selected?.label ?? "";
  const visible = useMemo(() => {
    if (!filter || query === null || query.trim() === "") return options;
    const q = query.trim().toLocaleLowerCase();
    return options.filter(
      (o) =>
        o.label.toLocaleLowerCase().includes(q) || o.description?.toLocaleLowerCase().includes(q),
    );
  }, [options, query, filter]);

  const optionId = (i: number) => `${listId}-opt-${i}`;
  const clamp = (i: number) => Math.max(0, Math.min(visible.length - 1, i));

  function choose(option: ComboboxOption) {
    onValueChange(option.value);
    setQuery(null);
    setOpen(false);
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    switch (e.key) {
      case "ArrowDown":
        e.preventDefault();
        if (!open) setOpen(true);
        else setActive((i) => clamp(i + 1));
        break;
      case "ArrowUp":
        e.preventDefault();
        if (!open) setOpen(true);
        else setActive((i) => clamp(i - 1));
        break;
      case "Home":
        if (open) {
          e.preventDefault();
          setActive(0);
        }
        break;
      case "End":
        if (open) {
          e.preventDefault();
          setActive(clamp(visible.length - 1));
        }
        break;
      case "Enter": {
        const option = visible[active];
        if (open && option) {
          e.preventDefault();
          choose(option);
        }
        break;
      }
      case "Escape":
        if (open) {
          e.preventDefault();
          setOpen(false);
          setQuery(null);
        }
        break;
      case "Tab":
        setOpen(false);
        setQuery(null);
        break;
    }
  }

  return (
    <Popover.Root open={open && !field.disabled} onOpenChange={setOpen}>
      <Popover.Anchor asChild>
        <div className={cn("relative flex items-center", className)}>
          {start ? (
            <span className="pointer-events-none absolute inset-s-3 flex items-center text-ink-muted">
              {start}
            </span>
          ) : null}
          <input
            ref={inputRef}
            {...field}
            type="text"
            role="combobox"
            autoComplete="off"
            aria-expanded={open}
            aria-controls={open ? listId : undefined}
            aria-autocomplete="list"
            aria-activedescendant={open && visible[active] ? optionId(active) : undefined}
            placeholder={placeholder}
            value={text}
            onFocus={() => setOpen(true)}
            onClick={() => setOpen(true)}
            onKeyDown={onKeyDown}
            onChange={(e) => {
              setQuery(e.target.value);
              setActive(0);
              setOpen(true);
              onQueryChange?.(e.target.value);
              if (value && e.target.value === "") onValueChange(null);
            }}
            className={cn(
              controlBase,
              "rounded-sm text-body",
              size === "lg" ? "h-12" : "h-10",
              start ? "ps-10" : "ps-3",
              "pe-10",
            )}
          />
          <span className="absolute inset-e-2 flex items-center gap-1 text-ink-muted">
            {loading ? <Spinner size={16} label={loadingLabel} /> : null}
            {clearLabel && (selected || text) && !loading ? (
              <button
                type="button"
                aria-label={clearLabel}
                className="grid size-6 place-items-center rounded-xs hover:bg-ocean/8"
                onClick={() => {
                  onValueChange(null);
                  setQuery("");
                  onQueryChange?.("");
                  inputRef.current?.focus();
                }}
              >
                <Icon name="x" size={14} />
              </button>
            ) : (
              !loading && <Icon name="chevron-down" size={18} className="pointer-events-none" />
            )}
          </span>
          {name ? <input type="hidden" name={name} value={value ?? ""} /> : null}
        </div>
      </Popover.Anchor>
      <Popover.Portal>
        <Popover.Content
          align="start"
          sideOffset={6}
          onOpenAutoFocus={(e) => e.preventDefault()}
          onInteractOutside={(e) => {
            if (inputRef.current?.contains(e.target as Node)) e.preventDefault();
          }}
          className="z-50 max-h-72 w-[var(--radix-popover-trigger-width)] overflow-auto rounded-md bg-surface p-1.5 shadow-[var(--shadow-hairline),var(--shadow-float)] animate-[dh-pop-in_var(--duration-base)_var(--ease-calm)]"
        >
          <ul id={listId} role="listbox" aria-label={placeholder}>
            {visible.map((o, i) => (
              <li
                key={o.value}
                id={optionId(i)}
                role="option"
                aria-selected={o.value === value}
                data-active={i === active || undefined}
                onMouseMove={() => setActive(i)}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => choose(o)}
                className="flex cursor-default items-center gap-3 rounded-sm px-3 py-2 text-body text-ink data-[active]:bg-lagoon-50"
              >
                {o.icon ? <Icon name={o.icon} size={20} className="text-lagoon-600" /> : null}
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-medium">{o.label}</span>
                  {o.description ? (
                    <span className="block truncate text-caption text-ink-muted">
                      {o.description}
                    </span>
                  ) : null}
                </span>
                {o.value === value ? (
                  <Icon name="check" size={16} className="text-lagoon-600" />
                ) : null}
              </li>
            ))}
          </ul>
          {visible.length === 0 && !loading ? (
            <p className="px-3 py-3 text-small text-ink-muted">{emptyLabel}</p>
          ) : null}
          <p className="sr-only" role="status" aria-live="polite">
            {resultsLabel(visible.length)}
          </p>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  );
}
